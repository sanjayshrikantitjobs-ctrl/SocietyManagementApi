using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Maintenance;

public class MaintenanceKpisDto
{
    public int TotalFlats { get; set; }
    public int BillsGenerated { get; set; }
    public int Paid { get; set; }
    public int Pending { get; set; }
    public int Overdue { get; set; }
    public decimal TotalCollection { get; set; }
    public decimal Outstanding { get; set; }
}

public class MonthlyCollectionPointDto
{
    public string MonthLabel { get; set; } = default!;
    public decimal Amount { get; set; }
}

public class PaidVsPendingDto
{
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class OutstandingByWingPointDto
{
    public string WingName { get; set; } = default!;
    public decimal Outstanding { get; set; }
}

public class RecentPaymentDto
{
    public int Id { get; set; }
    public string FlatNumber { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMode PaymentMode { get; set; }
}

public class OverdueFlatDto
{
    public int BillId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public string InvoiceNumber { get; set; } = default!;
    public decimal Balance { get; set; }
    public int DaysOverdue { get; set; }
}

public class MaintenanceDashboardDto
{
    public MaintenanceKpisDto Kpis { get; set; } = new();
    public List<MonthlyCollectionPointDto> MonthlyCollectionTrend { get; set; } = new();
    public PaidVsPendingDto PaidVsPending { get; set; } = new();
    public List<OutstandingByWingPointDto> OutstandingByWing { get; set; } = new();
    public List<RecentPaymentDto> RecentPayments { get; set; } = new();
    public List<OverdueFlatDto> OverdueFlats { get; set; } = new();
}

/// <summary>Month is optional — defaults to the current calendar month, same
/// as before this became selectable. Every KPI card (and the Overdue Flats
/// table below it) scopes to whichever month is selected; the
/// Outstanding-by-Wing table deliberately stays unscoped (a current-state
/// snapshot, not tied to one month). Pass Year instead of Month to scope
/// every KPI card and the trend chart to a full calendar year (Jan–Dec)
/// rather than one month — Year takes precedence when both are given.</summary>
public record GetMaintenanceDashboardQuery(int SocietyId, DateTime? Month = null, int? Year = null) : IRequest<MaintenanceDashboardDto>;

public class MaintenanceDashboardQueryHandler : IRequestHandler<GetMaintenanceDashboardQuery, MaintenanceDashboardDto>
{
    private readonly IApplicationDbContext _context;

    public MaintenanceDashboardQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<MaintenanceDashboardDto> Handle(GetMaintenanceDashboardQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var selectedMonth = request.Month.HasValue
            ? new DateTime(request.Month.Value.Year, request.Month.Value.Month, 1)
            : currentMonth;
        var selectedYear = request.Year;

        var totalFlats = await _context.Flats
            .CountAsync(f => !f.IsDeleted && f.Floor.Wing.Building.SocietyId == request.SocietyId, ct);

        var allBills = await _context.MaintenanceBills
            .Where(b => !b.IsDeleted && b.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(b => new { b.Id, b.FlatId, b.Status, b.DueDate, b.TotalAmount, b.AmountPaid, b.PreviousBalance, b.BillMonth, b.InvoiceNumber })
            .ToListAsync(ct);

        // Year, when given, scopes every KPI card to the whole calendar year
        // (Jan–Dec) instead of one month — Year takes precedence over Month.
        var currentMonthBills = selectedYear.HasValue
            ? allBills.Where(b => b.BillMonth.Year == selectedYear.Value).ToList()
            : allBills.Where(b => b.BillMonth == selectedMonth).ToList();

        var unpaidBills = currentMonthBills.Where(b => b.Status != BillStatus.Paid).ToList();
        var overdueBills = unpaidBills.Where(b => b.DueDate < today).ToList();

        // TotalAmount/AmountPaid are running cumulative figures (see
        // GenerateMonthlyBillsCommand's doc comment), so neither "money collected
        // this month" nor "still owed for this month" can be read off them directly
        // — both have to come from each bill's own line items and its own
        // MaintenancePayment rows specifically, the same ground truth the
        // reconciliation logic uses elsewhere. Otherwise this KPI silently includes
        // whatever an earlier month's bill carried forward into this one.
        var currentMonthBillIds = currentMonthBills.Select(b => b.Id).ToHashSet();
        var paidByBillThisMonth = currentMonthBillIds.Count == 0
            ? new Dictionary<int, decimal>()
            : (await _context.MaintenancePayments
                .Where(p => !p.IsDeleted && currentMonthBillIds.Contains(p.MaintenanceBillId))
                .Select(p => new { p.MaintenanceBillId, p.Amount })
                .ToListAsync(ct))
              .GroupBy(p => p.MaintenanceBillId)
              .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var totalCollectionThisMonth = paidByBillThisMonth.Values.Sum();
        var outstandingThisMonth = currentMonthBills.Sum(b =>
            Math.Max(0, (b.TotalAmount - b.PreviousBalance) - paidByBillThisMonth.GetValueOrDefault(b.Id, 0)));

        var kpis = new MaintenanceKpisDto
        {
            TotalFlats = totalFlats,
            BillsGenerated = currentMonthBills.Count,
            Paid = currentMonthBills.Count(b => b.Status == BillStatus.Paid),
            Pending = currentMonthBills.Count(b => b.Status != BillStatus.Paid && b.DueDate >= today),
            Overdue = overdueBills.Count,
            TotalCollection = totalCollectionThisMonth,
            Outstanding = outstandingThisMonth
        };

        // Year mode shows the full Jan–Dec trend for that year; month mode
        // keeps the existing rolling 6-month window ending at the current
        // (not necessarily selected) month.
        var trendStart = selectedYear.HasValue ? new DateTime(selectedYear.Value, 1, 1) : currentMonth.AddMonths(-5);
        var trendMonthCount = selectedYear.HasValue ? 12 : 6;
        var trendEndExclusive = trendStart.AddMonths(trendMonthCount);

        var monthlyCollection = await _context.MaintenancePayments
            .Where(p => !p.IsDeleted && p.MaintenanceBill.Flat.Floor.Wing.Building.SocietyId == request.SocietyId
                && p.PaymentDate >= trendStart && p.PaymentDate < trendEndExclusive)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var monthlyTrend = Enumerable.Range(0, trendMonthCount)
            .Select(offset => trendStart.AddMonths(offset))
            .Select(month =>
            {
                var match = monthlyCollection.FirstOrDefault(m => m.Year == month.Year && m.Month == month.Month);
                return new MonthlyCollectionPointDto { MonthLabel = month.ToString(selectedYear.HasValue ? "MMM" : "MMM yyyy"), Amount = match?.Amount ?? 0 };
            })
            .ToList();

        var paidVsPending = new PaidVsPendingDto
        {
            PaidAmount = kpis.TotalCollection,
            OutstandingAmount = kpis.Outstanding
        };

        var outstandingByWing = await _context.MaintenanceBills
            .Where(b => !b.IsDeleted && !b.IsRolledForward && b.Status != BillStatus.Paid && b.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .GroupBy(b => b.Flat.Floor.Wing.Name)
            .Select(g => new OutstandingByWingPointDto { WingName = g.Key, Outstanding = g.Sum(b => b.TotalAmount - b.AmountPaid) })
            .OrderByDescending(x => x.Outstanding)
            .ToListAsync(ct);

        var recentPayments = await _context.MaintenancePayments
            .Where(p => !p.IsDeleted && p.MaintenanceBill.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .OrderByDescending(p => p.PaymentDate)
            .Take(10)
            .Select(p => new RecentPaymentDto
            {
                Id = p.Id, FlatNumber = p.MaintenanceBill.Flat.FlatNumber, Amount = p.Amount,
                PaymentDate = p.PaymentDate, PaymentMode = p.PaymentMode
            })
            .ToListAsync(ct);

        var overdueBillIds = overdueBills
            .OrderByDescending(b => (today - b.DueDate.Date).Days)
            .Take(10)
            .Select(b => b.Id)
            .ToList();

        // DaysOverdue is computed in memory after materializing — DateDiff is a
        // SQL-Server-specific EF.Functions extension that would require the
        // Application layer to reference the SqlServer provider package,
        // breaking the Clean Architecture provider-agnostic boundary.
        var overdueFlatsById = (await _context.MaintenanceBills
            .Where(b => overdueBillIds.Contains(b.Id))
            .Select(b => new { b.Id, FlatNumber = b.Flat.FlatNumber, b.InvoiceNumber, b.TotalAmount, b.AmountPaid, b.DueDate })
            .ToListAsync(ct))
            .ToDictionary(b => b.Id, b => new OverdueFlatDto
            {
                BillId = b.Id, FlatNumber = b.FlatNumber, InvoiceNumber = b.InvoiceNumber,
                Balance = b.TotalAmount - b.AmountPaid, DaysOverdue = (today - b.DueDate.Date).Days
            });
        // Preserve the "most overdue first" ordering from overdueBillIds.
        var overdueFlats = overdueBillIds.Select(id => overdueFlatsById[id]).ToList();

        return new MaintenanceDashboardDto
        {
            Kpis = kpis,
            MonthlyCollectionTrend = monthlyTrend,
            PaidVsPending = paidVsPending,
            OutstandingByWing = outstandingByWing,
            RecentPayments = recentPayments,
            OverdueFlats = overdueFlats
        };
    }
}
