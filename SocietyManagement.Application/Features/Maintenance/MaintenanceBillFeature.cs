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
    /// <summary>True once this bill's balance has been carried into a later
    /// month's bill — it is no longer independently payable; any remaining
    /// debt should be settled against the newer bill instead.</summary>
    public bool IsRolledForward { get; set; }
    public string? PdfUrl { get; set; }
    public string? OwnerNameSnapshot { get; set; }
    /// <summary>Current owner/tenant, resolved live from FlatResidencies —
    /// distinct from OwnerNameSnapshot, which is frozen at bill-generation
    /// time and may be either depending who was primary contact then. Null
    /// when no primary-contact resident is on file for that role.</summary>
    public string? OwnerName { get; set; }
    public string? TenantName { get; set; }
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

/// <summary>Admin override for the reverse direction of RecordPaymentCommand
/// — corrects a bill that was marked paid in error. Voids (soft-deletes)
/// every payment recorded against this specific bill and resets it to
/// Pending; it does not touch any other bill for the flat, so if this
/// bill's balance had already been rolled forward into a later one (see
/// GenerateMonthlyBillsCommand's PreviousBalance), reopening it here is a
/// deliberate admin call, not something this command tries to untangle.</summary>
public record SetBillUnpaidCommand(int MaintenanceBillId) : IRequest<Unit>;

/// <summary>Bulk sibling of SetBillUnpaidCommand, mirroring
/// BulkRecordPaymentCommand's shape exactly — never throws for one bad id,
/// skips it with a reason instead.</summary>
public record BulkSetBillsUnpaidResultDto(int MaintenanceBillId, string InvoiceNumber, bool Reversed, string? SkipReason);

public record BulkSetBillsUnpaidCommand(List<int> MaintenanceBillIds) : IRequest<List<BulkSetBillsUnpaidResultDto>>;

public class BulkSetBillsUnpaidCommandValidator : AbstractValidator<BulkSetBillsUnpaidCommand>
{
    public BulkSetBillsUnpaidCommandValidator()
    {
        RuleFor(x => x.MaintenanceBillIds).NotEmpty();
    }
}

public class MaintenanceBillCommandHandlers :
    IRequestHandler<GenerateMonthlyBillsCommand, int>,
    IRequestHandler<RecordPaymentCommand, int>,
    IRequestHandler<BulkRecordPaymentCommand, List<BulkRecordPaymentResultDto>>,
    IRequestHandler<SetBillUnpaidCommand, Unit>,
    IRequestHandler<BulkSetBillsUnpaidCommand, List<BulkSetBillsUnpaidResultDto>>,
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

            // Total/AmountPaid are running cumulative figures — "everything
            // this flat has ever been billed / paid, through this month" —
            // not just this one month's own charges. The most recent prior
            // bill already holds last month's cumulative totals (regardless
            // of whether it was ever marked Paid), so this month's own
            // charge and any new payment simply add on top of that running
            // figure. Flag the prior bill superseded rather than closing it
            // as Paid — it was never independently payable again once a
            // newer bill exists.
            var mostRecentPrior = await _context.MaintenanceBills
                .Where(b => b.FlatId == flat.Id && !b.IsDeleted && b.BillMonth < billMonth)
                .OrderByDescending(b => b.BillMonth)
                .FirstOrDefaultAsync(ct);
            var previousBalance = mostRecentPrior?.TotalAmount ?? 0;
            // Capped at previousBalance: an overpayment on some earlier bill
            // must not silently carry forward as a credit that forgives a
            // brand-new month's charge before it's even due — each month's
            // own charge only counts as paid by money actually applied to
            // it or carried as genuine unpaid arrears, never by unrelated
            // excess sitting further back in the history.
            var previousPaid = Math.Min(mostRecentPrior?.AmountPaid ?? 0, previousBalance);
            if (mostRecentPrior != null) mostRecentPrior.IsRolledForward = true;

            var chargeTotal = lineItems.Sum(i => i.Amount);
            var totalAmount = chargeTotal + previousBalance;

            var invoiceNumber = $"{settings.InvoiceNumberPrefix}-{billMonth:yyyyMM}-{settings.NextInvoiceNumber:D4}";
            settings.NextInvoiceNumber++;

            var bill = new MaintenanceBill
            {
                SocietyId = request.SocietyId, FlatId = flat.Id, BillMonth = billMonth, InvoiceNumber = invoiceNumber,
                PreviousBalance = previousBalance, FineAmount = fineTotal, TotalAmount = totalAmount,
                AmountPaid = previousPaid, DueDate = dueDate,
                Status = previousPaid >= totalAmount ? BillStatus.Paid : previousPaid > 0 ? BillStatus.PartiallyPaid : BillStatus.Pending,
                GeneratedAt = DateTime.UtcNow, OwnerNameSnapshot = ownerName, OwnerPhoneSnapshot = ownerPhone
            };
            foreach (var item in lineItems) bill.Items.Add(item);

            await _context.MaintenanceBills.AddAsync(bill, ct);
            await _context.SaveChangesAsync(ct);

            // Bills are normally generated in calendar order, but if an
            // earlier month's bill is created after a later one already
            // exists (e.g. catching up a missed month), that later bill's
            // Total/AmountPaid were computed without this one and are now
            // stale. Cascade the recompute forward.
            await PropagateForwardAsync(flat.Id, billMonth, ct);

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
        if (bill.IsRolledForward)
        {
            throw new ConflictAppException("This bill's balance has been carried into a later bill — record the payment against that bill instead.");
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
        await PropagateForwardAsync(bill.FlatId, bill.BillMonth, ct);
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
            if (bill.IsRolledForward)
            {
                results.Add(new BulkRecordPaymentResultDto(id, bill.InvoiceNumber, false, "Balance carried into a later bill — pay that bill instead."));
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

        foreach (var bill in results.Where(r => r.Recorded).Select(r => billsById[r.MaintenanceBillId]))
        {
            await PropagateForwardAsync(bill.FlatId, bill.BillMonth, ct);
        }

        // EntityId is capped at 50 chars (see AuditLogConfiguration) — a
        // comma-joined id list overflows it past ~a dozen bills and throws
        // *after* the payments above already committed, so the request 500s
        // even though the data saved fine. Keep EntityId short; put the
        // actual ids in NewValues (unbounded JSON column) instead.
        var recordedIds = results.Where(r => r.Recorded).Select(r => r.MaintenanceBillId).ToList();
        await _auditService.LogAsync(AuditAction.Payment, "Maintenance", nameof(MaintenanceBill),
            $"bulk:{recordedIds.Count}", newValues: new { MaintenanceBillIds = recordedIds }, ct: ct);

        return results;
    }

    public async Task<Unit> Handle(SetBillUnpaidCommand request, CancellationToken ct)
    {
        var bill = await _context.MaintenanceBills.FirstOrDefaultAsync(b => b.Id == request.MaintenanceBillId && !b.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(MaintenanceBill), request.MaintenanceBillId);

        var payments = await _context.MaintenancePayments
            .Where(p => p.MaintenanceBillId == bill.Id && !p.IsDeleted)
            .ToListAsync(ct);
        foreach (var payment in payments) payment.IsDeleted = true;

        bill.AmountPaid = 0;
        bill.Status = BillStatus.Pending;
        await RecalculateBalanceAsync(bill, ct);

        await _context.SaveChangesAsync(ct);
        await PropagateForwardAsync(bill.FlatId, bill.BillMonth, ct);
        await _auditService.LogAsync(AuditAction.Update, "Maintenance", nameof(MaintenanceBill), bill.Id.ToString(),
            newValues: new { MarkedUnpaid = true, VoidedPaymentCount = payments.Count }, ct: ct);
        return Unit.Value;
    }

    public async Task<List<BulkSetBillsUnpaidResultDto>> Handle(BulkSetBillsUnpaidCommand request, CancellationToken ct)
    {
        var bills = await _context.MaintenanceBills
            .Where(b => request.MaintenanceBillIds.Contains(b.Id) && !b.IsDeleted)
            .ToListAsync(ct);
        var billsById = bills.ToDictionary(b => b.Id);

        var payments = await _context.MaintenancePayments
            .Where(p => !p.IsDeleted && request.MaintenanceBillIds.Contains(p.MaintenanceBillId))
            .ToListAsync(ct);
        var paymentsByBillId = payments.GroupBy(p => p.MaintenanceBillId).ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<BulkSetBillsUnpaidResultDto>();
        foreach (var id in request.MaintenanceBillIds)
        {
            if (!billsById.TryGetValue(id, out var bill))
            {
                results.Add(new BulkSetBillsUnpaidResultDto(id, "", false, "Bill not found."));
                continue;
            }
            if (bill.Status == BillStatus.Pending)
            {
                results.Add(new BulkSetBillsUnpaidResultDto(id, bill.InvoiceNumber, false, "Already unpaid."));
                continue;
            }

            if (paymentsByBillId.TryGetValue(id, out var billPayments))
            {
                foreach (var payment in billPayments) payment.IsDeleted = true;
            }
            bill.AmountPaid = 0;
            bill.Status = BillStatus.Pending;
            await RecalculateBalanceAsync(bill, ct);
            results.Add(new BulkSetBillsUnpaidResultDto(id, bill.InvoiceNumber, true, null));
        }

        await _context.SaveChangesAsync(ct);

        foreach (var bill in results.Where(r => r.Reversed).Select(r => billsById[r.MaintenanceBillId]))
        {
            await PropagateForwardAsync(bill.FlatId, bill.BillMonth, ct);
        }

        // Same EntityId-length pitfall as BulkRecordPaymentCommand (see its
        // comment) — kept short here from the start.
        var reversedIds = results.Where(r => r.Reversed).Select(r => r.MaintenanceBillId).ToList();
        await _auditService.LogAsync(AuditAction.Update, "Maintenance", nameof(MaintenanceBill),
            $"bulk-unpaid:{reversedIds.Count}", newValues: new { MaintenanceBillIds = reversedIds }, ct: ct);

        return results;
    }

    /// <summary>Recomputes Total/AmountPaid for a bill being reopened via
    /// Mark Unpaid, against the flat's *current* billing history — not the
    /// frozen snapshot taken when this bill was originally generated.
    /// Total/AmountPaid are running cumulative figures (see
    /// GenerateMonthlyBillsCommand's own doc comment on this), so reopening
    /// this bill must also re-derive them from whatever the most recent
    /// prior bill currently holds, the same way generation does — otherwise
    /// a stale cumulative figure from before some other correction would
    /// stick around forever. Only this bill's own payments are voided by
    /// the caller before this runs; everything the flat paid in earlier
    /// months stays credited, same as it would on a freshly generated
    /// bill.</summary>
    private async Task RecalculateBalanceAsync(MaintenanceBill bill, CancellationToken ct)
    {
        var ownCharges = await _context.MaintenanceBillItems
            .Where(i => i.MaintenanceBillId == bill.Id && !i.IsDeleted)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0;
        var ownPaid = await _context.MaintenancePayments
            .Where(p => p.MaintenanceBillId == bill.Id && !p.IsDeleted)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        var mostRecentPrior = await _context.MaintenanceBills
            .Where(b => b.FlatId == bill.FlatId && b.Id != bill.Id && !b.IsDeleted && b.BillMonth < bill.BillMonth)
            .OrderByDescending(b => b.BillMonth)
            .FirstOrDefaultAsync(ct);
        var previousBalance = mostRecentPrior?.TotalAmount ?? 0;
        // Capped at previousBalance — see GenerateMonthlyBillsCommand's own
        // comment on this: an overpayment further back in the history must
        // not silently forgive the month being reopened here.
        var previousPaid = Math.Min(mostRecentPrior?.AmountPaid ?? 0, previousBalance);
        if (mostRecentPrior != null) mostRecentPrior.IsRolledForward = true;

        bill.PreviousBalance = previousBalance;
        bill.TotalAmount = ownCharges + previousBalance;
        bill.AmountPaid = ownPaid + previousPaid;
        bill.Status = bill.AmountPaid >= bill.TotalAmount ? BillStatus.Paid
            : bill.AmountPaid > 0 ? BillStatus.PartiallyPaid : BillStatus.Pending;
    }

    /// <summary>Whenever a bill's own Total/AmountPaid changes — a new bill
    /// is inserted earlier in the flat's history, a payment lands, a bill
    /// is reopened — any bill *already existing* for a later month has a
    /// cumulative Total/AmountPaid computed from what this bill used to
    /// hold, not what it holds now. Cascades RecalculateBalanceAsync
    /// forward across every later bill for the flat, oldest first, so each
    /// one picks up the (by then already-updated) figures from the one
    /// immediately before it.</summary>
    private async Task PropagateForwardAsync(int flatId, DateTime fromBillMonthInclusive, CancellationToken ct)
    {
        var laterBills = await _context.MaintenanceBills
            .Where(b => b.FlatId == flatId && !b.IsDeleted && b.BillMonth > fromBillMonthInclusive)
            .OrderBy(b => b.BillMonth)
            .ToListAsync(ct);
        if (laterBills.Count == 0) return;

        foreach (var laterBill in laterBills)
        {
            await RecalculateBalanceAsync(laterBill, ct);
        }
        await _context.SaveChangesAsync(ct);
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

/// <summary>Same filters as GetBillsQuery, unpaginated — every matching bill
/// is exported, not just the current page.</summary>
public record GetBillsExportPdfQuery(int SocietyId, BillStatus? Status, DateTime? BillMonth) : IRequest<byte[]>;

public record GetBillsExportExcelQuery(int SocietyId, BillStatus? Status, DateTime? BillMonth) : IRequest<byte[]>;

public class MaintenanceBillQueryHandlers :
    IRequestHandler<GetBillsQuery, PaginatedResult<MaintenanceBillDto>>,
    IRequestHandler<GetBillByIdQuery, MaintenanceBillDetailDto>,
    IRequestHandler<GetBillPdfQuery, byte[]>,
    IRequestHandler<GetBillsExportPdfQuery, byte[]>,
    IRequestHandler<GetBillsExportExcelQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly IMaintenanceBillPdfService _pdfService;
    private readonly IMaintenanceBillsExportService _exportService;

    public MaintenanceBillQueryHandlers(
        IApplicationDbContext context, IMaintenanceBillPdfService pdfService, IMaintenanceBillsExportService exportService)
    {
        _context = context;
        _pdfService = pdfService;
        _exportService = exportService;
    }

    private static MaintenanceBillDto Project(MaintenanceBill b) => new()
    {
        Id = b.Id, FlatId = b.FlatId, FlatNumber = b.Flat.FlatNumber, BuildingName = b.Flat.Floor.Wing.Building.Name,
        WingName = b.Flat.Floor.Wing.Name, BillMonth = b.BillMonth, InvoiceNumber = b.InvoiceNumber,
        PreviousBalance = b.PreviousBalance, FineAmount = b.FineAmount, TotalAmount = b.TotalAmount,
        AmountPaid = b.AmountPaid, DueDate = b.DueDate, Status = BillStatusDisplay.Compute(b.Status, b.DueDate),
        IsRolledForward = b.IsRolledForward, PdfUrl = b.PdfUrl, OwnerNameSnapshot = b.OwnerNameSnapshot
    };

    /// <summary>Shared by the paginated list query and both unpaginated
    /// export queries — same filters, same owner/tenant enrichment, so the
    /// export always matches exactly what the list screen would show for
    /// the same filters, just without a page cut.</summary>
    private async Task<(List<MaintenanceBillDto> Items, int TotalCount)> GetFilteredDtosAsync(
        int societyId, int? flatId, BillStatus? status, DateTime? billMonth, int? skip, int? take, CancellationToken ct)
    {
        var query = _context.MaintenanceBills
            .Where(b => !b.IsDeleted && b.SocietyId == societyId);

        if (flatId.HasValue) query = query.Where(b => b.FlatId == flatId);
        if (billMonth.HasValue)
        {
            var month = new DateTime(billMonth.Value.Year, billMonth.Value.Month, 1);
            query = query.Where(b => b.BillMonth == month);
        }

        // Status is filtered here as SQL-translatable predicates equivalent to
        // BillStatusDisplay.Compute, so pagination/totalCount stay correct —
        // filtering the *computed* display status in memory after Skip/Take
        // would desync the count and the page contents.
        var today = DateTime.UtcNow.Date;
        query = status switch
        {
            BillStatus.Overdue => query.Where(b => b.Status != BillStatus.Paid && b.DueDate < today),
            BillStatus.Paid => query.Where(b => b.Status == BillStatus.Paid),
            BillStatus.Pending or BillStatus.PartiallyPaid =>
                query.Where(b => b.Status == status && b.DueDate >= today),
            _ => query
        };

        var totalCount = await query.CountAsync(ct);

        var ordered = query
            .Include(b => b.Flat).ThenInclude(f => f.Floor).ThenInclude(fl => fl.Wing).ThenInclude(w => w.Building)
            .OrderByDescending(b => b.BillMonth).ThenBy(b => b.FlatId);
        var paged = skip.HasValue && take.HasValue ? ordered.Skip(skip.Value).Take(take.Value) : ordered;

        var items = await paged.ToListAsync(ct);
        var dtos = items.Select(Project).ToList();

        var flatIds = dtos.Select(d => d.FlatId).Distinct().ToList();
        var primaryContacts = await _context.FlatResidencies
            .Where(r => !r.IsDeleted && r.MoveOutDate == null && r.IsPrimaryContact
                && (r.MemberType == MemberType.Owner || r.MemberType == MemberType.Tenant) && flatIds.Contains(r.FlatId))
            .Select(r => new { r.FlatId, r.MemberType, Name = r.Member.FirstName + " " + r.Member.LastName })
            .ToListAsync(ct);
        var ownerNamesByFlat = primaryContacts.Where(r => r.MemberType == MemberType.Owner).ToDictionary(r => r.FlatId, r => r.Name);
        var tenantNamesByFlat = primaryContacts.Where(r => r.MemberType == MemberType.Tenant).ToDictionary(r => r.FlatId, r => r.Name);

        foreach (var dto in dtos)
        {
            dto.OwnerName = ownerNamesByFlat.GetValueOrDefault(dto.FlatId);
            dto.TenantName = tenantNamesByFlat.GetValueOrDefault(dto.FlatId);
        }

        return (dtos, totalCount);
    }

    public async Task<PaginatedResult<MaintenanceBillDto>> Handle(GetBillsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var (dtos, totalCount) = await GetFilteredDtosAsync(
            request.SocietyId, request.FlatId, request.Status, request.BillMonth,
            (pageNumber - 1) * pageSize, pageSize, ct);

        return new PaginatedResult<MaintenanceBillDto>(dtos, totalCount, pageNumber, pageSize);
    }

    private async Task<MaintenanceBillsExportData> BuildExportDataAsync(
        int societyId, BillStatus? status, DateTime? billMonth, CancellationToken ct)
    {
        var (dtos, _) = await GetFilteredDtosAsync(societyId, null, status, billMonth, null, null, ct);

        var society = await _context.Societies.FirstOrDefaultAsync(s => s.Id == societyId, ct);
        var monthLabel = billMonth.HasValue ? billMonth.Value.ToString("MMMM yyyy") : "All Months";
        var statusLabel = status.HasValue ? status.Value.ToString() : "All Statuses";

        return new MaintenanceBillsExportData
        {
            SocietyName = society?.Name ?? "Society",
            FilterLabel = $"{monthLabel} — {statusLabel}",
            Rows = dtos.Select(d => new MaintenanceBillExportRow
            {
                FlatNumber = d.FlatNumber, BuildingName = d.BuildingName, WingName = d.WingName,
                OwnerName = d.OwnerName, TenantName = d.TenantName, InvoiceNumber = d.InvoiceNumber,
                BillMonth = d.BillMonth, TotalAmount = d.TotalAmount, AmountPaid = d.AmountPaid,
                Balance = d.Balance, DueDate = d.DueDate, StatusLabel = d.Status.ToString()
            }).ToList()
        };
    }

    public async Task<byte[]> Handle(GetBillsExportPdfQuery request, CancellationToken ct)
    {
        var data = await BuildExportDataAsync(request.SocietyId, request.Status, request.BillMonth, ct);
        return _exportService.GeneratePdf(data);
    }

    public async Task<byte[]> Handle(GetBillsExportExcelQuery request, CancellationToken ct)
    {
        var data = await BuildExportDataAsync(request.SocietyId, request.Status, request.BillMonth, ct);
        return _exportService.GenerateExcel(data);
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
            AmountPaid = dto.AmountPaid, DueDate = dto.DueDate, Status = dto.Status, IsRolledForward = dto.IsRolledForward, PdfUrl = dto.PdfUrl,
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
