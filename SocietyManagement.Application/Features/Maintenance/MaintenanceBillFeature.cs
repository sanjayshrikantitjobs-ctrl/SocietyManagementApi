using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Maintenance;

public class MaintenanceBillDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public string BuildingName { get; set; } = default!;
    public string WingName { get; set; } = default!;
    public DateTime BillMonth { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public decimal PreviousBalance { get; set; }
    public decimal FineAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance => TotalAmount - AmountPaid;
    public DateTime DueDate { get; set; }
    public BillStatus Status { get; set; }
    public string? PdfUrl { get; set; }
    public string? OwnerNameSnapshot { get; set; }
}

public class MaintenanceBillItemDto
{
    public int Id { get; set; }
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }
    public BillItemType ItemType { get; set; }
}

public class MaintenancePaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
}

public class MaintenanceBillDetailDto : MaintenanceBillDto
{
    public List<MaintenanceBillItemDto> Items { get; set; } = new();
    public List<MaintenancePaymentDto> Payments { get; set; } = new();
}

/// <summary>Stored Status only ever tracks payment progress
/// (Pending/PartiallyPaid/Paid) — "Overdue" is derived at read time from
/// DueDate so it can never go stale across a day boundary without a sweep job.</summary>
internal static class BillStatusDisplay
{
    public static BillStatus Compute(BillStatus storedStatus, DateTime dueDate) =>
        storedStatus == BillStatus.Paid
            ? BillStatus.Paid
            : dueDate.Date < DateTime.UtcNow.Date ? BillStatus.Overdue : storedStatus;
}

// ---- Commands ----------------------------------------------------------------
public record GenerateMonthlyBillsCommand(int SocietyId, DateTime? BillMonth) : IRequest<int>;

public record RecordPaymentCommand(
    int MaintenanceBillId, decimal Amount, DateTime PaymentDate, PaymentMode PaymentMode,
    string? TransactionReference, string? Notes) : IRequest<int>;

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.MaintenanceBillId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

/// <summary>Pays each selected bill's own full outstanding balance —
/// mode/date/notes are shared across the batch, but Amount is never a
/// client-supplied shared value (balances differ per flat). Never throws for
/// one bad id; skips it with a reason instead, so one already-paid or
/// deleted bill in a large selection doesn't abort the whole batch.</summary>
public record BulkRecordPaymentResultDto(int MaintenanceBillId, string InvoiceNumber, bool Recorded, string? SkipReason);

public record BulkRecordPaymentCommand(
    List<int> MaintenanceBillIds, DateTime PaymentDate, PaymentMode PaymentMode,
    string? TransactionReference, string? Notes) : IRequest<List<BulkRecordPaymentResultDto>>;

public class BulkRecordPaymentCommandValidator : AbstractValidator<BulkRecordPaymentCommand>
{
    public BulkRecordPaymentCommandValidator()
    {
        RuleFor(x => x.MaintenanceBillIds).NotEmpty();
    }
}

public record ResendBillWhatsAppCommand(int MaintenanceBillId) : IRequest<Unit>;

public class MaintenanceBillCommandHandlers :
    IRequestHandler<GenerateMonthlyBillsCommand, int>,
    IRequestHandler<RecordPaymentCommand, int>,
    IRequestHandler<BulkRecordPaymentCommand, List<BulkRecordPaymentResultDto>>,
    IRequestHandler<ResendBillWhatsAppCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;
    private readonly IMaintenanceBillPdfService _pdfService;
    private readonly IFileStorageService _fileStorage;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<MaintenanceBillCommandHandlers> _logger;

    public MaintenanceBillCommandHandlers(
        IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser,
        IMaintenanceBillPdfService pdfService, IFileStorageService fileStorage, IWhatsAppService whatsAppService,
        ILogger<MaintenanceBillCommandHandlers> logger)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
        _pdfService = pdfService;
        _fileStorage = fileStorage;
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    public async Task<int> Handle(GenerateMonthlyBillsCommand request, CancellationToken ct)
    {
        var society = await _context.Societies.FirstOrDefaultAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Society), request.SocietyId);

        var settings = await _context.MaintenanceSettings.FirstOrDefaultAsync(s => s.SocietyId == request.SocietyId && !s.IsDeleted, ct)
            ?? throw new ConflictAppException("Configure maintenance settings for this society before generating bills.");

        var billMonthRaw = (request.BillMonth ?? DateTime.UtcNow).Date;
        var billMonth = new DateTime(billMonthRaw.Year, billMonthRaw.Month, 1);

        var flats = await _context.Flats
            .Where(f => !f.IsDeleted && f.Floor.Wing.Building.SocietyId == request.SocietyId)
            .ToListAsync(ct);

        var alreadyBilledFlatIds = await _context.MaintenanceBills
            .Where(b => !b.IsDeleted && b.BillMonth == billMonth && b.SocietyId == request.SocietyId)
            .Select(b => b.FlatId)
            .ToListAsync(ct);

        var flatsToProcess = flats.Where(f => !alreadyBilledFlatIds.Contains(f.Id)).ToList();
        if (flatsToProcess.Count == 0) return 0;

        var categories = await _context.MaintenanceCategories
            .Where(c => c.SocietyId == request.SocietyId && c.IsActive && !c.IsDeleted && c.EffectiveFrom <= billMonth)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(ct);

        // Prefer the flat's current primary-contact resident (real Member
        // data) over the Flat.OwnerName/Phone stopgap fields, which now only
        // matter as a fallback for flats with no Resident Management data yet.
        var primaryContacts = await _context.FlatResidencies
            .Where(r => !r.IsDeleted && r.MoveOutDate == null && r.IsPrimaryContact &&
                flatsToProcess.Select(f => f.Id).Contains(r.FlatId))
            .Select(r => new { r.FlatId, Name = r.Member.FirstName + " " + r.Member.LastName, r.Member.Phone })
            .ToDictionaryAsync(r => r.FlatId, ct);

        var dueDate = new DateTime(billMonth.Year, billMonth.Month,
            Math.Min(settings.DueDay, DateTime.DaysInMonth(billMonth.Year, billMonth.Month)));

        var generatedCount = 0;

        foreach (var flat in flatsToProcess)
        {
            var hasPrimaryContact = primaryContacts.TryGetValue(flat.Id, out var primaryContact);
            var ownerName = hasPrimaryContact ? primaryContact!.Name : flat.OwnerName;
            var ownerPhone = hasPrimaryContact ? primaryContact!.Phone : flat.OwnerPhone;

            var lineItems = new List<MaintenanceBillItem>();

            foreach (var category in categories)
            {
                if (category.ChargeType == ChargeType.OneTime)
                {
                    var alreadyBilledOnce = await _context.MaintenanceBillItems
                        .AnyAsync(i => i.MaintenanceCategoryId == category.Id && i.MaintenanceBill.FlatId == flat.Id, ct);
                    if (alreadyBilledOnce) continue;
                }

                var amount = category.ChargeType == ChargeType.PerSqFt && flat.AreaSqFt.HasValue
                    ? category.MonthlyAmount * flat.AreaSqFt.Value
                    : category.MonthlyAmount;

                lineItems.Add(new MaintenanceBillItem
                {
                    Description = category.ChargeName, Amount = amount,
                    ItemType = BillItemType.Category, MaintenanceCategoryId = category.Id
                });
            }

            var specialCharges = await _context.SpecialCharges
                .Where(sc => sc.FlatId == flat.Id && sc.IsActive && !sc.IsDeleted &&
                    sc.StartDate <= billMonth && (sc.EndDate == null || sc.EndDate >= billMonth))
                .ToListAsync(ct);

            foreach (var sc in specialCharges)
            {
                lineItems.Add(new MaintenanceBillItem
                {
                    Description = sc.ChargeName, Amount = sc.Amount,
                    ItemType = BillItemType.SpecialCharge, SpecialChargeId = sc.Id
                });
                if (sc.Frequency == ChargeFrequency.OneTime) sc.IsActive = false;
            }

            var pendingFines = await _context.FineRecords
                .Where(f => f.FlatId == flat.Id && f.Status == FineStatus.Pending && !f.IsDeleted)
                .ToListAsync(ct);

            decimal fineTotal = 0;
            foreach (var fine in pendingFines)
            {
                lineItems.Add(new MaintenanceBillItem
                {
                    Description = fine.Reason, Amount = fine.Amount,
                    ItemType = BillItemType.Fine, FineRecordId = fine.Id
                });
                fineTotal += fine.Amount;
                fine.Status = FineStatus.Billed;
            }

            var priorUnpaidBills = await _context.MaintenanceBills
                .Where(b => b.FlatId == flat.Id && !b.IsDeleted && b.Status != BillStatus.Paid)
                .ToListAsync(ct);
            var previousBalance = priorUnpaidBills.Sum(b => b.TotalAmount - b.AmountPaid);

            var chargeTotal = lineItems.Sum(i => i.Amount);
            var totalAmount = chargeTotal + previousBalance;

            var invoiceNumber = $"{settings.InvoiceNumberPrefix}-{billMonth:yyyyMM}-{settings.NextInvoiceNumber:D4}";
            settings.NextInvoiceNumber++;

            var bill = new MaintenanceBill
            {
                SocietyId = request.SocietyId, FlatId = flat.Id, BillMonth = billMonth, InvoiceNumber = invoiceNumber,
                PreviousBalance = previousBalance, FineAmount = fineTotal, TotalAmount = totalAmount,
                AmountPaid = 0, DueDate = dueDate, Status = BillStatus.Pending, GeneratedAt = DateTime.UtcNow,
                OwnerNameSnapshot = ownerName, OwnerPhoneSnapshot = ownerPhone
            };
            foreach (var item in lineItems) bill.Items.Add(item);

            await _context.MaintenanceBills.AddAsync(bill, ct);
            await _context.SaveChangesAsync(ct);

            var pdfBytes = _pdfService.GenerateBillPdf(BuildPdfData(society, settings, flat.FlatNumber, bill));
            bill.PdfUrl = await _fileStorage.SaveAsync(pdfBytes, $"{invoiceNumber}.pdf", "maintenance-bills", ct);
            await _context.SaveChangesAsync(ct);

            if (!settings.WhatsAppEnabled)
            {
                _logger.LogInformation(
                    "WhatsApp sending is disabled for society {SocietyId} — bill {InvoiceNumber} generated but not sent.",
                    request.SocietyId, invoiceNumber);
            }
            else if (!string.IsNullOrWhiteSpace(ownerPhone))
            {
                var message = settings.WhatsAppMessageTemplate
                    .Replace("{OwnerName}", ownerName ?? "Resident")
                    .Replace("{Month}", billMonth.ToString("MMMM yyyy"))
                    .Replace("{Amount}", $"Rs. {totalAmount:N0}")
                    .Replace("{DueDate}", dueDate.ToString("dd MMM"));
                await _whatsAppService.SendWhatsAppDocumentAsync(ownerPhone, message, pdfBytes, $"{invoiceNumber}.pdf", ct);
            }
            else
            {
                _logger.LogWarning(
                    "Flat {FlatId} has no resident contact on file (no current primary FlatResidency, no Flat.OwnerPhone) — bill {InvoiceNumber} generated but not sent via WhatsApp.",
                    flat.Id, invoiceNumber);
            }

            generatedCount++;
        }

        await _auditService.LogAsync(AuditAction.Create, "Maintenance", nameof(MaintenanceBill),
            $"{request.SocietyId}:{billMonth:yyyy-MM}", ct: ct);

        return generatedCount;
    }

    public async Task<int> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        var bill = await _context.MaintenanceBills.FirstOrDefaultAsync(b => b.Id == request.MaintenanceBillId && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(MaintenanceBill), request.MaintenanceBillId);

        if (bill.Status == BillStatus.Paid)
        {
            throw new ConflictAppException("This bill is already fully paid.");
        }

        var payment = new MaintenancePayment
        {
            MaintenanceBillId = bill.Id, Amount = request.Amount, PaymentDate = request.PaymentDate,
            PaymentMode = request.PaymentMode, TransactionReference = request.TransactionReference,
            ReceivedByUserId = _currentUser.UserId, Notes = request.Notes
        };
        await _context.MaintenancePayments.AddAsync(payment, ct);

        bill.AmountPaid += request.Amount;
        bill.Status = bill.AmountPaid >= bill.TotalAmount ? BillStatus.Paid : BillStatus.PartiallyPaid;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Payment, "Maintenance", nameof(MaintenanceBill), bill.Id.ToString(), ct: ct);
        return payment.Id;
    }

    public async Task<List<BulkRecordPaymentResultDto>> Handle(BulkRecordPaymentCommand request, CancellationToken ct)
    {
        var bills = await _context.MaintenanceBills
            .Where(b => request.MaintenanceBillIds.Contains(b.Id) && !b.IsDeleted)
            .ToListAsync(ct);
        var billsById = bills.ToDictionary(b => b.Id);

        var results = new List<BulkRecordPaymentResultDto>();
        foreach (var id in request.MaintenanceBillIds)
        {
            if (!billsById.TryGetValue(id, out var bill))
            {
                results.Add(new BulkRecordPaymentResultDto(id, "", false, "Bill not found."));
                continue;
            }
            if (bill.Status == BillStatus.Paid)
            {
                results.Add(new BulkRecordPaymentResultDto(id, bill.InvoiceNumber, false, "Already fully paid."));
                continue;
            }

            var amount = bill.TotalAmount - bill.AmountPaid;
            var payment = new MaintenancePayment
            {
                MaintenanceBillId = bill.Id, Amount = amount, PaymentDate = request.PaymentDate,
                PaymentMode = request.PaymentMode, TransactionReference = request.TransactionReference,
                ReceivedByUserId = _currentUser.UserId, Notes = request.Notes
            };
            await _context.MaintenancePayments.AddAsync(payment, ct);

            bill.AmountPaid += amount;
            bill.Status = BillStatus.Paid;
            results.Add(new BulkRecordPaymentResultDto(id, bill.InvoiceNumber, true, null));
        }

        await _context.SaveChangesAsync(ct);
        var recordedIds = results.Where(r => r.Recorded).Select(r => r.MaintenanceBillId.ToString());
        await _auditService.LogAsync(AuditAction.Payment, "Maintenance", nameof(MaintenanceBill), string.Join(",", recordedIds), ct: ct);

        return results;
    }

    public async Task<Unit> Handle(ResendBillWhatsAppCommand request, CancellationToken ct)
    {
        var bill = await _context.MaintenanceBills.Include(b => b.Flat)
            .FirstOrDefaultAsync(b => b.Id == request.MaintenanceBillId && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(MaintenanceBill), request.MaintenanceBillId);

        if (string.IsNullOrWhiteSpace(bill.OwnerPhoneSnapshot))
        {
            throw new ConflictAppException("This flat has no owner phone number on file.");
        }

        var society = await _context.Societies.FirstAsync(s => s.Id == bill.SocietyId, ct);
        var settings = await _context.MaintenanceSettings.FirstAsync(s => s.SocietyId == society.Id && !s.IsDeleted, ct);

        if (!settings.WhatsAppEnabled)
        {
            throw new ConflictAppException("WhatsApp sending is currently disabled for this society.");
        }

        var pdfBytes = _pdfService.GenerateBillPdf(BuildPdfData(society, settings, bill.Flat.FlatNumber, bill));

        var message = settings.WhatsAppMessageTemplate
            .Replace("{OwnerName}", bill.OwnerNameSnapshot ?? "Resident")
            .Replace("{Month}", bill.BillMonth.ToString("MMMM yyyy"))
            .Replace("{Amount}", $"Rs. {bill.TotalAmount:N0}")
            .Replace("{DueDate}", bill.DueDate.ToString("dd MMM"));

        await _whatsAppService.SendWhatsAppDocumentAsync(bill.OwnerPhoneSnapshot, message, pdfBytes, $"{bill.InvoiceNumber}.pdf", ct);
        return Unit.Value;
    }

    internal static MaintenanceBillPdfData BuildPdfData(Society society, MaintenanceSettings settings, string flatNumber, MaintenanceBill bill) =>
        new(
            society.Name, society.Address, society.LogoUrl, bill.InvoiceNumber, bill.BillMonth.ToString("MMMM yyyy"),
            flatNumber, bill.OwnerNameSnapshot,
            bill.Items.Select(i => new MaintenanceBillPdfItem(i.Description, i.Amount)).ToList(),
            bill.PreviousBalance, bill.FineAmount, bill.TotalAmount, bill.DueDate, settings.PdfFooterMessage);
}

// ---- Queries -------------------------------------------------------------------
public record GetBillsQuery(
    int SocietyId, int? FlatId, BillStatus? Status, DateTime? BillMonth,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<MaintenanceBillDto>>;

public record GetBillByIdQuery(int Id) : IRequest<MaintenanceBillDetailDto>;

public record GetBillPdfQuery(int Id) : IRequest<byte[]>;

public class MaintenanceBillQueryHandlers :
    IRequestHandler<GetBillsQuery, PaginatedResult<MaintenanceBillDto>>,
    IRequestHandler<GetBillByIdQuery, MaintenanceBillDetailDto>,
    IRequestHandler<GetBillPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly IMaintenanceBillPdfService _pdfService;

    public MaintenanceBillQueryHandlers(IApplicationDbContext context, IMaintenanceBillPdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
    }

    private static MaintenanceBillDto Project(MaintenanceBill b) => new()
    {
        Id = b.Id, FlatId = b.FlatId, FlatNumber = b.Flat.FlatNumber, BuildingName = b.Flat.Floor.Wing.Building.Name,
        WingName = b.Flat.Floor.Wing.Name, BillMonth = b.BillMonth, InvoiceNumber = b.InvoiceNumber,
        PreviousBalance = b.PreviousBalance, FineAmount = b.FineAmount, TotalAmount = b.TotalAmount,
        AmountPaid = b.AmountPaid, DueDate = b.DueDate, Status = BillStatusDisplay.Compute(b.Status, b.DueDate),
        PdfUrl = b.PdfUrl, OwnerNameSnapshot = b.OwnerNameSnapshot
    };

    public async Task<PaginatedResult<MaintenanceBillDto>> Handle(GetBillsQuery request, CancellationToken ct)
    {
        var query = _context.MaintenanceBills
            .Where(b => !b.IsDeleted && b.SocietyId == request.SocietyId);

        if (request.FlatId.HasValue) query = query.Where(b => b.FlatId == request.FlatId);
        if (request.BillMonth.HasValue)
        {
            var month = new DateTime(request.BillMonth.Value.Year, request.BillMonth.Value.Month, 1);
            query = query.Where(b => b.BillMonth == month);
        }

        // Status is filtered here as SQL-translatable predicates equivalent to
        // BillStatusDisplay.Compute, so pagination/totalCount stay correct —
        // filtering the *computed* display status in memory after Skip/Take
        // would desync the count and the page contents.
        var today = DateTime.UtcNow.Date;
        query = request.Status switch
        {
            BillStatus.Overdue => query.Where(b => b.Status != BillStatus.Paid && b.DueDate < today),
            BillStatus.Paid => query.Where(b => b.Status == BillStatus.Paid),
            BillStatus.Pending or BillStatus.PartiallyPaid =>
                query.Where(b => b.Status == request.Status && b.DueDate >= today),
            _ => query
        };

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .Include(b => b.Flat).ThenInclude(f => f.Floor).ThenInclude(fl => fl.Wing).ThenInclude(w => w.Building)
            .OrderByDescending(b => b.BillMonth).ThenBy(b => b.FlatId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<MaintenanceBillDto>(items.Select(Project).ToList(), totalCount, pageNumber, pageSize);
    }

    public async Task<MaintenanceBillDetailDto> Handle(GetBillByIdQuery request, CancellationToken ct)
    {
        var bill = await _context.MaintenanceBills
            .Include(b => b.Flat).ThenInclude(f => f.Floor).ThenInclude(fl => fl.Wing).ThenInclude(w => w.Building)
            .Include(b => b.Items)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(MaintenanceBill), request.Id);

        var dto = Project(bill);
        return new MaintenanceBillDetailDto
        {
            Id = dto.Id, FlatId = dto.FlatId, FlatNumber = dto.FlatNumber, BuildingName = dto.BuildingName,
            WingName = dto.WingName, BillMonth = dto.BillMonth, InvoiceNumber = dto.InvoiceNumber,
            PreviousBalance = dto.PreviousBalance, FineAmount = dto.FineAmount, TotalAmount = dto.TotalAmount,
            AmountPaid = dto.AmountPaid, DueDate = dto.DueDate, Status = dto.Status, PdfUrl = dto.PdfUrl,
            OwnerNameSnapshot = dto.OwnerNameSnapshot,
            Items = bill.Items.Where(i => !i.IsDeleted).Select(i => new MaintenanceBillItemDto
            {
                Id = i.Id, Description = i.Description, Amount = i.Amount, ItemType = i.ItemType
            }).ToList(),
            Payments = bill.Payments.Where(p => !p.IsDeleted).OrderByDescending(p => p.PaymentDate).Select(p => new MaintenancePaymentDto
            {
                Id = p.Id, Amount = p.Amount, PaymentDate = p.PaymentDate, PaymentMode = p.PaymentMode,
                TransactionReference = p.TransactionReference, Notes = p.Notes
            }).ToList()
        };
    }

    public async Task<byte[]> Handle(GetBillPdfQuery request, CancellationToken ct)
    {
        var bill = await _context.MaintenanceBills
            .Include(b => b.Flat)
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(MaintenanceBill), request.Id);

        var society = await _context.Societies.FirstAsync(s => s.Id == bill.SocietyId, ct);
        var settings = await _context.MaintenanceSettings.FirstAsync(s => s.SocietyId == bill.SocietyId && !s.IsDeleted, ct);

        return _pdfService.GenerateBillPdf(
            MaintenanceBillCommandHandlers.BuildPdfData(society, settings, bill.Flat.FlatNumber, bill));
    }
}
