using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Common.Models;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalContributionDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public int? FlatId { get; set; }
    public string? FlatNumber { get; set; }
    public string MemberName { get; set; } = default!;
    public decimal Amount { get; set; }
    public ContributionPaymentMethod PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? TransactionId { get; set; }
    public string ReceiptNumber { get; set; } = default!;
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? WhatsAppNumber { get; set; }
}

public class TopContributorDto
{
    public string MemberName { get; set; } = default!;
    public string? FlatNumber { get; set; }
    public int? FlatId { get; set; }
    public decimal TotalAmount { get; set; }
    public int ContributionCount { get; set; }
}

public class PendingContributorDto
{
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
}

// ---- Commands ----------------------------------------------------------------
public record CreateContributionCommand(
    int FestivalId, int? FlatId, string MemberName, decimal Amount, ContributionPaymentMethod PaymentMethod,
    DateTime PaymentDate, string? TransactionId, bool IsAnonymous, string? WhatsAppNumber) : IRequest<int>;

public class CreateContributionCommandValidator : AbstractValidator<CreateContributionCommand>
{
    public CreateContributionCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.MemberName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.TransactionId).MaximumLength(100);
        RuleFor(x => x.WhatsAppNumber).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber))
            .WithMessage("A valid 10-digit mobile number is required.");
    }
}

/// <summary>Editing an already-recorded contribution — FestivalId/FlatId
/// never change (those define which record this is), only the payment
/// details themselves. No re-send of the WhatsApp receipt on edit — that
/// stays an explicit, separate action via ResendContributionReceiptCommand.</summary>
public record UpdateContributionCommand(
    int Id, string MemberName, decimal Amount, ContributionPaymentMethod PaymentMethod,
    DateTime PaymentDate, string? TransactionId, bool IsAnonymous) : IRequest<Unit>;

public class UpdateContributionCommandValidator : AbstractValidator<UpdateContributionCommand>
{
    public UpdateContributionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.MemberName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.TransactionId).MaximumLength(100);
    }
}

/// <summary>Removes a wrongly-recorded contribution entirely (e.g. a test
/// entry, or one logged against the wrong flat) — as opposed to
/// UpdateContributionCommand, which corrects one still meant to stand. Since
/// FlatContributionStatus/PaidAmount are computed live from the sum of
/// non-deleted contributions, deleting one is all "reverting" a payment
/// requires — no separate status field to flip back.</summary>
public record DeleteContributionCommand(int Id) : IRequest<Unit>;

public record ResendContributionReceiptCommand(int ContributionId, string? WhatsAppNumber) : IRequest<Unit>;

public class ResendContributionReceiptCommandValidator : AbstractValidator<ResendContributionReceiptCommand>
{
    public ResendContributionReceiptCommandValidator()
    {
        RuleFor(x => x.ContributionId).GreaterThan(0);
        RuleFor(x => x.WhatsAppNumber).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber))
            .WithMessage("A valid 10-digit mobile number is required.");
    }
}

public class ContributionCommandHandlers :
    IRequestHandler<CreateContributionCommand, int>,
    IRequestHandler<UpdateContributionCommand, Unit>,
    IRequestHandler<DeleteContributionCommand, Unit>,
    IRequestHandler<ResendContributionReceiptCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;
    private readonly IPdfReceiptService _pdfReceiptService;
    private readonly IWhatsAppService _whatsAppService;

    public ContributionCommandHandlers(
        IApplicationDbContext context, IAuditService auditService, INotificationService notificationService,
        IPdfReceiptService pdfReceiptService, IWhatsAppService whatsAppService)
    {
        _context = context;
        _auditService = auditService;
        _notificationService = notificationService;
        _pdfReceiptService = pdfReceiptService;
        _whatsAppService = whatsAppService;
    }

    public async Task<int> Handle(CreateContributionCommand request, CancellationToken ct)
    {
        var festivalSocietyId = await _context.Festivals
            .Where(f => f.Id == request.FestivalId && !f.IsDeleted)
            .Select(f => (int?)f.SocietyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        if (request.FlatId.HasValue &&
            !await _context.Flats.AnyAsync(fl => fl.Id == request.FlatId && !fl.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId.Value);
        }

        var whatsAppNumber = !string.IsNullOrWhiteSpace(request.WhatsAppNumber)
            ? request.WhatsAppNumber
            : request.FlatId.HasValue ? await ContributionReceiptHelper.ResolveFlatPhoneAsync(_context, request.FlatId.Value, ct) : null;

        var contribution = new FestivalContribution
        {
            FestivalId = request.FestivalId,
            FlatId = request.FlatId,
            MemberName = request.IsAnonymous ? "Anonymous Donor" : request.MemberName,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = request.PaymentDate,
            TransactionId = request.TransactionId,
            IsAnonymous = request.IsAnonymous,
            WhatsAppNumber = whatsAppNumber,
            ReceiptNumber = $"RCPT{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"
        };
        await _context.FestivalContributions.AddAsync(contribution, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Payment, "Festivals", nameof(FestivalContribution), contribution.Id.ToString(), ct: ct);

        var totalCollected = await _context.FestivalContributions
            .Where(c => c.FestivalId == request.FestivalId && !c.IsDeleted)
            .SumAsync(c => c.Amount, ct);
        // Was SendToAllAsync — a festival is one society's own event, not
        // something every user in the entire database should be notified
        // about; tenant-isolation fix, not a behavior change to what
        // "recording a contribution" means.
        await _notificationService.SendToSocietyAsync(festivalSocietyId, "FestivalContributionRecorded",
            new { festivalId = request.FestivalId, amount = contribution.Amount, totalCollected },
            "Contribution recorded", $"₹{contribution.Amount:0.##} recorded — ₹{totalCollected:0.##} collected so far",
            $"festival-contribution-{contribution.Id}-recorded", ct);

        if (!string.IsNullOrWhiteSpace(whatsAppNumber))
        {
            await ContributionReceiptHelper.SendReceiptWhatsAppAsync(_context, _pdfReceiptService, _whatsAppService, contribution.Id, whatsAppNumber, ct);
        }

        return contribution.Id;
    }

    public async Task<Unit> Handle(UpdateContributionCommand request, CancellationToken ct)
    {
        var contribution = await _context.FestivalContributions
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalContribution), request.Id);

        contribution.MemberName = request.IsAnonymous ? "Anonymous Donor" : request.MemberName;
        contribution.Amount = request.Amount;
        contribution.PaymentMethod = request.PaymentMethod;
        contribution.PaymentDate = request.PaymentDate;
        contribution.TransactionId = request.TransactionId;
        contribution.IsAnonymous = request.IsAnonymous;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalContribution), contribution.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteContributionCommand request, CancellationToken ct)
    {
        var contribution = await _context.FestivalContributions
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalContribution), request.Id);

        contribution.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalContribution), contribution.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ResendContributionReceiptCommand request, CancellationToken ct)
    {
        var contribution = await _context.FestivalContributions
            .FirstOrDefaultAsync(c => c.Id == request.ContributionId && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalContribution), request.ContributionId);

        var whatsAppNumber = !string.IsNullOrWhiteSpace(request.WhatsAppNumber)
            ? request.WhatsAppNumber
            : contribution.WhatsAppNumber ?? (contribution.FlatId.HasValue
                ? await ContributionReceiptHelper.ResolveFlatPhoneAsync(_context, contribution.FlatId.Value, ct)
                : null);

        if (string.IsNullOrWhiteSpace(whatsAppNumber))
        {
            throw new BadRequestAppException("No WhatsApp number on file for this contribution — enter one to resend.");
        }

        if (whatsAppNumber != contribution.WhatsAppNumber)
        {
            contribution.WhatsAppNumber = whatsAppNumber;
            await _context.SaveChangesAsync(ct);
        }

        await ContributionReceiptHelper.SendReceiptWhatsAppAsync(_context, _pdfReceiptService, _whatsAppService, contribution.Id, whatsAppNumber, ct);
        return Unit.Value;
    }
}

/// <summary>Shared by both the record-contribution and resend-receipt flows,
/// and mirrors MaintenanceBillFeature.cs's own primary-contact-then-fallback
/// phone resolution so both features treat "who does a flat's WhatsApp
/// receipt go to" the same way.</summary>
internal static class ContributionReceiptHelper
{
    public static async Task<string?> ResolveFlatPhoneAsync(IApplicationDbContext context, int flatId, CancellationToken ct)
    {
        var primaryContactPhone = await context.FlatResidencies
            .Where(r => !r.IsDeleted && r.MoveOutDate == null && r.IsPrimaryContact && r.FlatId == flatId)
            .Select(r => r.Member.Phone)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(primaryContactPhone)) return primaryContactPhone;

        return await context.Flats.Where(f => f.Id == flatId).Select(f => f.OwnerPhone).FirstOrDefaultAsync(ct);
    }

    public static async Task SendReceiptWhatsAppAsync(
        IApplicationDbContext context, IPdfReceiptService pdfReceiptService, IWhatsAppService whatsAppService,
        int contributionId, string whatsAppNumber, CancellationToken ct)
    {
        var receiptData = await ContributionQueryHandlers.BuildReceiptDataAsync(context, contributionId, ct);
        var pdfBytes = pdfReceiptService.GenerateContributionReceipt(receiptData);
        var fileName = $"{receiptData.ReceiptNumber}.pdf";
        var message = $"Thank you for your contribution of ₹{receiptData.Amount:N0} to {receiptData.FestivalName}! Receipt {receiptData.ReceiptNumber} is attached.";
        // Body params match both approved templates' {{1}}..{{4}} order:
        // resident name, amount, festival name, transaction/payment ID. Meta
        // rejects a template send outright (error 131008, "missing text
        // value") if any text parameter is blank — TransactionId is optional
        // and the create-contribution form can submit "" rather than a true
        // null when left empty, which `??` alone wouldn't catch.
        var bodyParams = new[]
        {
            receiptData.DonorName, receiptData.Amount.ToString("N0"), receiptData.FestivalName,
            string.IsNullOrWhiteSpace(receiptData.TransactionId) ? receiptData.ReceiptNumber : receiptData.TransactionId
        };

        // The document-header template is tried FIRST, not as a fallback:
        // Meta's synchronous response for a "direct" (session-only) send can
        // report success — a real 200 and wamid — even when the recipient has
        // no open 24h session and the message is never actually delivered.
        // Confirmed directly: a real direct send to a fresh number returned a
        // successful response but never arrived, while the identical PDF sent
        // via this same template (verified independently via Postman)
        // delivered correctly. So "direct succeeded" can't be trusted as
        // proof of delivery — the template is the reliable path and goes
        // first regardless of session state.
        if (await whatsAppService.SendWhatsAppDocumentTemplateAsync(
                whatsAppNumber, "society_festival_payment_receipt", "en_US", bodyParams, pdfBytes, fileName, ct))
        {
            return;
        }

        // Falls back to a direct send only if the template attempt itself
        // fails outright (e.g. a transient API error, or the template isn't
        // approved yet) — worth trying since it's free within an active
        // session, even though its "success" can't be trusted the other way.
        if (await whatsAppService.SendWhatsAppDocumentAsync(whatsAppNumber, message, pdfBytes, fileName, ct))
        {
            return;
        }

        // Last resort: festival_payment_success is body-only (no header), so
        // it can't carry the PDF — this at least confirms the payment instead
        // of the resident hearing nothing.
        await whatsAppService.SendWhatsAppTemplateAsync(whatsAppNumber, "festival_payment_success", "en", bodyParams, ct);
    }
}

// ---- Queries -------------------------------------------------------------------
/// <summary>FlatId powers the merged Contribution page's per-flat detail
/// view (one code path for both "all contributions" and "this flat's
/// history") — null means unfiltered, same as every other optional filter here.</summary>
public record GetContributionsQuery(
    int FestivalId, int? FlatId, string? Search, ContributionPaymentMethod? PaymentMethod,
    string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FestivalContributionDto>>;

/// <summary>The "sum after filters" companion to GetContributionsQuery —
/// same Search/PaymentMethod/FlatId filter, aggregated over every matching
/// row rather than just the current page.</summary>
public record GetContributionsSumQuery(int FestivalId, int? FlatId, string? Search, ContributionPaymentMethod? PaymentMethod)
    : IRequest<decimal>;

public record GetTopContributorsQuery(int FestivalId, int Top = 10) : IRequest<List<TopContributorDto>>;

public record GetPendingContributorsQuery(int FestivalId) : IRequest<List<PendingContributorDto>>;

public record GetContributionReceiptPdfQuery(int Id) : IRequest<byte[]>;

/// <summary>Same filters as GetContributionsQuery, unpaginated — every matching row is exported.</summary>
public record GetContributionsExportPdfQuery(int FestivalId, string? Search, ContributionPaymentMethod? PaymentMethod) : IRequest<byte[]>;

public record GetContributionsExportExcelQuery(int FestivalId, string? Search, ContributionPaymentMethod? PaymentMethod) : IRequest<byte[]>;

public class ContributionQueryHandlers :
    IRequestHandler<GetContributionsQuery, PaginatedResult<FestivalContributionDto>>,
    IRequestHandler<GetContributionsSumQuery, decimal>,
    IRequestHandler<GetTopContributorsQuery, List<TopContributorDto>>,
    IRequestHandler<GetPendingContributorsQuery, List<PendingContributorDto>>,
    IRequestHandler<GetContributionReceiptPdfQuery, byte[]>,
    IRequestHandler<GetContributionsExportPdfQuery, byte[]>,
    IRequestHandler<GetContributionsExportExcelQuery, byte[]>
{
    private readonly IApplicationDbContext _context;
    private readonly IPdfReceiptService _pdfReceiptService;
    private readonly IFlatContributionsExportService _exportService;

    public ContributionQueryHandlers(IApplicationDbContext context, IPdfReceiptService pdfReceiptService, IFlatContributionsExportService exportService)
    {
        _context = context;
        _pdfReceiptService = pdfReceiptService;
        _exportService = exportService;
    }

    /// <summary>Shared by GetContributionsQuery and its "sum" companion so
    /// the two can never disagree about which rows match a given filter.</summary>
    private IQueryable<FestivalContribution> ApplyFilters(int festivalId, int? flatId, string? search, ContributionPaymentMethod? paymentMethod)
    {
        var query = _context.FestivalContributions.Where(c => c.FestivalId == festivalId);

        if (flatId.HasValue) query = query.Where(c => c.FlatId == flatId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.MemberName.ToLower().Contains(term) || c.ReceiptNumber.ToLower().Contains(term));
        }

        if (paymentMethod.HasValue) query = query.Where(c => c.PaymentMethod == paymentMethod);

        return query;
    }

    public async Task<PaginatedResult<FestivalContributionDto>> Handle(GetContributionsQuery request, CancellationToken ct)
    {
        var query = ApplyFilters(request.FestivalId, request.FlatId, request.Search, request.PaymentMethod);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("donor", false) => query.OrderBy(c => c.MemberName),
            ("donor", true) => query.OrderByDescending(c => c.MemberName),
            ("amount", false) => query.OrderBy(c => c.Amount),
            ("amount", true) => query.OrderByDescending(c => c.Amount),
            ("method", false) => query.OrderBy(c => c.PaymentMethod),
            ("method", true) => query.OrderByDescending(c => c.PaymentMethod),
            ("date", false) => query.OrderBy(c => c.PaymentDate),
            ("date", true) => query.OrderByDescending(c => c.PaymentDate),
            ("receipt", false) => query.OrderBy(c => c.ReceiptNumber),
            ("receipt", true) => query.OrderByDescending(c => c.ReceiptNumber),
            // Default and "flat": guest/no-flat contributions (FlatId null) sort last.
            _ => query.OrderBy(c => c.FlatId == null).ThenBy(c => c.FlatId)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new FestivalContributionDto
            {
                Id = c.Id,
                FestivalId = c.FestivalId,
                FlatId = c.FlatId,
                FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null,
                MemberName = c.MemberName,
                Amount = c.Amount,
                PaymentMethod = c.PaymentMethod,
                PaymentDate = c.PaymentDate,
                TransactionId = c.TransactionId,
                ReceiptNumber = c.ReceiptNumber,
                IsAnonymous = c.IsAnonymous,
                CreatedAt = c.CreatedAt,
                WhatsAppNumber = c.WhatsAppNumber
            })
            .ToListAsync(ct);

        return new PaginatedResult<FestivalContributionDto>(items, totalCount, pageNumber, pageSize);
    }

    private async Task<ContributionLedgerExportData> BuildLedgerExportAsync(
        int festivalId, string? search, ContributionPaymentMethod? paymentMethod, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == festivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), festivalId);
        var society = await _context.Societies.FirstOrDefaultAsync(s => s.Id == festival.SocietyId, ct);

        var rows = await ApplyFilters(festivalId, null, search, paymentMethod)
            .OrderByDescending(c => c.PaymentDate).ThenByDescending(c => c.CreatedAt)
            .Select(c => new { c.MemberName, FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null, c.Amount, c.PaymentMethod, c.PaymentDate, c.ReceiptNumber, c.TransactionId })
            .ToListAsync(ct);

        var filters = new List<string>();
        if (paymentMethod.HasValue) filters.Add(paymentMethod.Value switch
        {
            ContributionPaymentMethod.Cash => "Cash",
            ContributionPaymentMethod.UPI => "UPI",
            _ => "Bank Transfer"
        });
        if (!string.IsNullOrWhiteSpace(search)) filters.Add($"Search: {search.Trim()}");

        return new ContributionLedgerExportData
        {
            SocietyName = society?.Name ?? "Society",
            FestivalName = festival.Name,
            FilterLabel = filters.Count > 0 ? string.Join(" · ", filters) : "All Payment Modes",
            TotalAmount = rows.Sum(r => r.Amount),
            Rows = rows.Select(r => new ContributionLedgerExportRow
            {
                Donor = r.MemberName, FlatNumber = r.FlatNumber, Amount = r.Amount,
                MethodLabel = r.PaymentMethod switch
                {
                    ContributionPaymentMethod.Cash => "Cash",
                    ContributionPaymentMethod.UPI => "UPI",
                    _ => "Bank Transfer"
                },
                PaymentDate = r.PaymentDate, ReceiptNumber = r.ReceiptNumber, TransactionId = r.TransactionId
            }).ToList()
        };
    }

    public async Task<byte[]> Handle(GetContributionsExportPdfQuery request, CancellationToken ct) =>
        _exportService.GenerateLedgerPdf(await BuildLedgerExportAsync(request.FestivalId, request.Search, request.PaymentMethod, ct));

    public async Task<byte[]> Handle(GetContributionsExportExcelQuery request, CancellationToken ct) =>
        _exportService.GenerateLedgerExcel(await BuildLedgerExportAsync(request.FestivalId, request.Search, request.PaymentMethod, ct));

    public async Task<decimal> Handle(GetContributionsSumQuery request, CancellationToken ct) =>
        await ApplyFilters(request.FestivalId, request.FlatId, request.Search, request.PaymentMethod).SumAsync(c => c.Amount, ct);

    public async Task<List<TopContributorDto>> Handle(GetTopContributorsQuery request, CancellationToken ct) =>
        await _context.FestivalContributions
            .Where(c => c.FestivalId == request.FestivalId && !c.IsAnonymous)
            .GroupBy(c => new { c.MemberName, FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null, c.FlatId })
            .Select(g => new TopContributorDto
            {
                MemberName = g.Key.MemberName,
                FlatNumber = g.Key.FlatNumber,
                FlatId = g.Key.FlatId,
                TotalAmount = g.Sum(c => c.Amount),
                ContributionCount = g.Count()
            })
            .OrderByDescending(c => c.TotalAmount)
            .Take(request.Top)
            .ToListAsync(ct);

    public async Task<List<PendingContributorDto>> Handle(GetPendingContributorsQuery request, CancellationToken ct)
    {
        var festival = await _context.Festivals.FirstOrDefaultAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Festival), request.FestivalId);

        var contributedFlatIds = await _context.FestivalContributions
            .Where(c => c.FestivalId == request.FestivalId && c.FlatId != null)
            .Select(c => c.FlatId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return await _context.Flats
            .Where(fl => fl.Floor.Wing.Building.SocietyId == festival.SocietyId && !contributedFlatIds.Contains(fl.Id))
            .Select(fl => new PendingContributorDto { FlatId = fl.Id, FlatNumber = fl.FlatNumber })
            .OrderBy(fl => fl.FlatId)
            .ToListAsync(ct);
    }

    public async Task<byte[]> Handle(GetContributionReceiptPdfQuery request, CancellationToken ct)
    {
        var data = await BuildReceiptDataAsync(_context, request.Id, ct);
        return _pdfReceiptService.GenerateContributionReceipt(data);
    }

    /// <summary>Shared with ContributionReceiptHelper.SendReceiptWhatsAppAsync
    /// (ContributionCommandHandlers) so the record/resend WhatsApp flows and
    /// this download endpoint always build the identical receipt.</summary>
    public static async Task<ContributionReceiptData> BuildReceiptDataAsync(IApplicationDbContext context, int contributionId, CancellationToken ct)
    {
        var data = await context.FestivalContributions
            .Where(c => c.Id == contributionId)
            .Select(c => new
            {
                c.ReceiptNumber,
                SocietyName = c.Festival.Society.Name,
                SocietyAddress = c.Festival.Society.Address,
                SocietyCity = c.Festival.Society.City,
                SocietyState = c.Festival.Society.State,
                SocietyPincode = c.Festival.Society.Pincode,
                SocietyLogoUrl = c.Festival.Society.LogoUrl,
                FestivalId = c.FestivalId,
                FestivalName = c.Festival.Name,
                FestivalYear = c.Festival.Year,
                c.MemberName,
                c.FlatId,
                FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null,
                c.Amount,
                c.PaymentMethod,
                c.PaymentDate,
                c.TransactionId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(FestivalContribution), contributionId);

        decimal? targetAmount = null;
        var totalPaidForFlat = data.Amount;
        if (data.FlatId.HasValue)
        {
            targetAmount = await context.FestivalFlatTargets
                .Where(t => t.FestivalId == data.FestivalId && t.FlatId == data.FlatId)
                .Select(t => (decimal?)t.TargetAmount)
                .FirstOrDefaultAsync(ct);

            totalPaidForFlat = await context.FestivalContributions
                .Where(c => c.FestivalId == data.FestivalId && c.FlatId == data.FlatId)
                .SumAsync(c => c.Amount, ct);
        }

        var societyAddress = $"{data.SocietyAddress}, {data.SocietyCity}, {data.SocietyState} - {data.SocietyPincode}";

        return new ContributionReceiptData(
            data.ReceiptNumber, data.SocietyName, societyAddress, data.SocietyLogoUrl, data.FestivalName, data.FestivalYear,
            data.MemberName, data.FlatNumber,
            data.Amount, data.PaymentMethod.ToString(), data.PaymentDate, data.TransactionId,
            targetAmount, totalPaidForFlat);
    }
}
