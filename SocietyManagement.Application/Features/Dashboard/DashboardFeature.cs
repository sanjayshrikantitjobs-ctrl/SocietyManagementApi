using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Dashboard;

public class PendingActionsDto
{
    public int OutstandingBillsCount { get; set; }
    public int OpenComplaintsCount { get; set; }
    public int VendorPaymentsDueCount { get; set; }
    public int WaterTankerPendingCount { get; set; }
    public int VisitorRequestsWaitingCount { get; set; }
}

/// <summary>Admin dashboard summary — scoped to one society (see
/// GetAdminDashboardSummaryQuery's doc comment for why this matters).</summary>
public class AdminDashboardSummaryDto
{
    public int TotalFlats { get; set; }
    public int OccupiedFlats { get; set; }
    public int OwnersCount { get; set; }
    public int TenantsCount { get; set; }
    public decimal CollectedMaintenance { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int OpenComplaintsCount { get; set; }
    public int VisitorsToday { get; set; }
    public PendingActionsDto PendingActions { get; set; } = new();
}

/// <summary>Previously took no SocietyId at all — every number was a
/// system-wide count across every society, not the caller's own. A scoped
/// Admin saw every other society's flats/collections/complaints mixed
/// into their numbers. Fixed here since every query below was being
/// rewritten anyway for the new KPI set.</summary>
public record GetAdminDashboardSummaryQuery(int SocietyId) : IRequest<AdminDashboardSummaryDto>;

public class GetAdminDashboardSummaryQueryHandler : IRequestHandler<GetAdminDashboardSummaryQuery, AdminDashboardSummaryDto>
{
    private static readonly ComplaintStatus[] OpenComplaintStatuses =
        { ComplaintStatus.Open, ComplaintStatus.Assigned, ComplaintStatus.InProgress };

    private readonly IApplicationDbContext _context;

    public GetAdminDashboardSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminDashboardSummaryDto> Handle(GetAdminDashboardSummaryQuery request, CancellationToken ct)
    {
        var societyId = request.SocietyId;
        var today = DateTime.UtcNow.Date;
        var currentMonth = new DateTime(today.Year, today.Month, 1);

        var totalFlats = await _context.Flats
            .CountAsync(f => !f.IsDeleted && f.Floor.Wing.Building.SocietyId == societyId, ct);
        // Flat.Status is a legacy field that isn't kept in sync with the
        // Occupancy module (see ResidentsOverviewFeature's own doc comment) —
        // occupancy must be derived from FlatOccupancy, same as OwnersCount/
        // TenantsCount below, or this disagrees with them on the same dashboard.
        var occupiedFlats = await _context.FlatOccupancies
            .Where(o => !o.IsDeleted && o.EndDate == null && o.Flat.Floor.Wing.Building.SocietyId == societyId)
            .Select(o => o.FlatId)
            .Distinct()
            .CountAsync(ct);

        var ownersCount = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate == null && m.FlatOccupancy.Type == OccupancyType.Owner
                && m.FlatOccupancy.EndDate == null && m.FlatOccupancy.Flat.Floor.Wing.Building.SocietyId == societyId)
            .Select(m => m.FlatOccupancy.FlatId)
            .Distinct()
            .CountAsync(ct);
        var tenantsCount = await _context.OccupancyMembers
            .Where(m => !m.IsDeleted && m.LeftDate == null && m.FlatOccupancy.Type == OccupancyType.Tenant
                && m.FlatOccupancy.EndDate == null && m.FlatOccupancy.Flat.Floor.Wing.Building.SocietyId == societyId)
            .Select(m => m.FlatOccupancy.FlatId)
            .Distinct()
            .CountAsync(ct);

        var unpaidBills = await _context.MaintenanceBills
            .Where(b => !b.IsDeleted && !b.IsRolledForward && b.Status != BillStatus.Paid && b.Flat.Floor.Wing.Building.SocietyId == societyId)
            .Select(b => b.TotalAmount - b.AmountPaid)
            .ToListAsync(ct);
        var outstandingBillsCount = unpaidBills.Count;

        var collectedMaintenance = await _context.MaintenancePayments
            .Where(p => !p.IsDeleted && p.MaintenanceBill.Flat.Floor.Wing.Building.SocietyId == societyId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        var openComplaintsCount = await _context.Complaints
            .CountAsync(c => !c.IsDeleted && c.SocietyId == societyId && OpenComplaintStatuses.Contains(c.Status), ct);

        var visitorsToday = await _context.VisitorVisits
            .CountAsync(v => !v.IsDeleted && v.SocietyId == societyId && v.RequestedAt.Date == today, ct);
        var visitorRequestsWaiting = await _context.VisitorVisits
            .CountAsync(v => !v.IsDeleted && v.SocietyId == societyId && v.Status == VisitorVisitStatus.PendingApproval, ct);

        var vendorPaymentsDueCount = await _context.FestivalVendors
            .Where(v => !v.IsDeleted && v.SocietyId == societyId)
            .CountAsync(v => v.Expenses.Where(e => !e.IsDeleted && e.ApprovalStatus == ExpenseApprovalStatus.Approved)
                .Sum(e => (decimal?)e.Amount) > 0, ct);

        var waterTankerPendingCount = await _context.WaterTankerCollections
            .CountAsync(w => !w.IsDeleted && w.SocietyId == societyId && w.Month == currentMonth && !w.IsPaid, ct);

        return new AdminDashboardSummaryDto
        {
            TotalFlats = totalFlats,
            OccupiedFlats = occupiedFlats,
            OwnersCount = ownersCount,
            TenantsCount = tenantsCount,
            CollectedMaintenance = collectedMaintenance,
            OutstandingAmount = unpaidBills.Sum(),
            OpenComplaintsCount = openComplaintsCount,
            VisitorsToday = visitorsToday,
            PendingActions = new PendingActionsDto
            {
                OutstandingBillsCount = outstandingBillsCount,
                OpenComplaintsCount = openComplaintsCount,
                VendorPaymentsDueCount = vendorPaymentsDueCount,
                WaterTankerPendingCount = waterTankerPendingCount,
                VisitorRequestsWaitingCount = visitorRequestsWaiting
            }
        };
    }
}

public class MonthlyCollectionPointDto
{
    public string MonthLabel { get; set; } = default!;
    public decimal Collected { get; set; }
    public decimal Pending { get; set; }
}

public record GetMonthlyCollectionTrendQuery(int SocietyId, int Months = 6) : IRequest<List<MonthlyCollectionPointDto>>;

public class GetMonthlyCollectionTrendQueryHandler : IRequestHandler<GetMonthlyCollectionTrendQuery, List<MonthlyCollectionPointDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMonthlyCollectionTrendQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<MonthlyCollectionPointDto>> Handle(GetMonthlyCollectionTrendQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var months = Enumerable.Range(0, request.Months)
            .Select(i => new DateTime(today.Year, today.Month, 1).AddMonths(-i))
            .OrderBy(m => m)
            .ToList();
        var rangeStart = months[0];

        var payments = await _context.MaintenancePayments
            .Where(p => !p.IsDeleted && p.PaymentDate >= rangeStart
                && p.MaintenanceBill.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(p => new { p.PaymentDate, p.Amount })
            .ToListAsync(ct);

        var unpaidBills = await _context.MaintenanceBills
            .Where(b => !b.IsDeleted && b.Status != BillStatus.Paid && b.BillMonth >= rangeStart
                && b.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .Select(b => new { b.BillMonth, Balance = b.TotalAmount - b.AmountPaid })
            .ToListAsync(ct);

        return months.Select(m => new MonthlyCollectionPointDto
        {
            MonthLabel = m.ToString("MMM"),
            Collected = payments.Where(p => p.PaymentDate.Year == m.Year && p.PaymentDate.Month == m.Month).Sum(p => p.Amount),
            Pending = unpaidBills.Where(b => b.BillMonth.Year == m.Year && b.BillMonth.Month == m.Month).Sum(b => b.Balance)
        }).ToList();
    }
}

public class UpcomingFestivalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Budget { get; set; }
    public decimal Collected { get; set; }
}

public class UpcomingEventDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public DateTime EventDateTime { get; set; }
    public string? Venue { get; set; }
}

public class UpcomingItemsDto
{
    public UpcomingFestivalDto? Festival { get; set; }
    public List<UpcomingEventDto> Events { get; set; } = new();
}

public record GetUpcomingItemsQuery(int SocietyId) : IRequest<UpcomingItemsDto>;

public class GetUpcomingItemsQueryHandler : IRequestHandler<GetUpcomingItemsQuery, UpcomingItemsDto>
{
    private readonly IApplicationDbContext _context;

    public GetUpcomingItemsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<UpcomingItemsDto> Handle(GetUpcomingItemsQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var festival = await _context.Festivals
            .Where(f => !f.IsDeleted && f.SocietyId == request.SocietyId
                && f.Status != FestivalStatus.Completed && f.EndDate >= today)
            .OrderBy(f => f.EndDate)
            .Select(f => new UpcomingFestivalDto
            {
                Id = f.Id, Name = f.Name, StartDate = f.StartDate, EndDate = f.EndDate,
                Budget = f.BudgetCategories.Sum(c => (decimal?)c.ApprovedAmount) ?? 0,
                Collected = f.Contributions.Where(c => !c.IsDeleted).Sum(c => (decimal?)c.Amount) ?? 0
            })
            .FirstOrDefaultAsync(ct);

        var events = await _context.Events
            .Where(e => !e.IsDeleted && e.SocietyId == request.SocietyId
                && e.EventDateTime >= today && e.Status != EventStatus.Cancelled && e.Status != EventStatus.Completed)
            .OrderBy(e => e.EventDateTime)
            .Take(3)
            .Select(e => new UpcomingEventDto { Id = e.Id, Name = e.Name, EventDateTime = e.EventDateTime, Venue = e.Venue })
            .ToListAsync(ct);

        return new UpcomingItemsDto { Festival = festival, Events = events };
    }
}

public class RecentActivityItemDto
{
    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Subtitle { get; set; }
    public DateTime Timestamp { get; set; }
}

public record GetRecentActivityQuery(int SocietyId, int Take = 10) : IRequest<List<RecentActivityItemDto>>;

/// <summary>Deliberately not sourced from AuditLog — that table has no
/// SocietyId column and is written from dozens of call sites across every
/// module, so scoping it would mean touching all of them. Merges the top
/// candidates from four already-scoped tables instead.</summary>
public class GetRecentActivityQueryHandler : IRequestHandler<GetRecentActivityQuery, List<RecentActivityItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRecentActivityQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<RecentActivityItemDto>> Handle(GetRecentActivityQuery request, CancellationToken ct)
    {
        var take = request.Take;

        var payments = await _context.MaintenancePayments
            .Where(p => !p.IsDeleted && p.MaintenanceBill.Flat.Floor.Wing.Building.SocietyId == request.SocietyId)
            .OrderByDescending(p => p.PaymentDate)
            .Take(take)
            .Select(p => new RecentActivityItemDto
            {
                Type = "payment", Title = $"₹{p.Amount:N0} payment received",
                Subtitle = "Flat " + p.MaintenanceBill.Flat.FlatNumber, Timestamp = p.PaymentDate
            })
            .ToListAsync(ct);

        var complaints = await _context.Complaints
            .Where(c => !c.IsDeleted && c.SocietyId == request.SocietyId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .Select(c => new RecentActivityItemDto
            {
                Type = "complaint", Title = "Complaint created",
                Subtitle = "Flat " + c.Flat.FlatNumber + " · " + c.Title, Timestamp = c.CreatedAt
            })
            .ToListAsync(ct);

        var visits = await _context.VisitorVisits
            .Where(v => !v.IsDeleted && v.SocietyId == request.SocietyId)
            .OrderByDescending(v => v.RequestedAt)
            .Take(take)
            .Select(v => new RecentActivityItemDto
            {
                Type = "visitor", Title = "Visitor entry",
                Subtitle = "Flat " + v.Flat.FlatNumber, Timestamp = v.RequestedAt
            })
            .ToListAsync(ct);

        var waterTanker = await _context.WaterTankerCollections
            .Where(w => !w.IsDeleted && w.SocietyId == request.SocietyId && w.IsPaid && w.PaymentDate != null)
            .OrderByDescending(w => w.PaymentDate)
            .Take(take)
            .Select(w => new RecentActivityItemDto
            {
                Type = "watertanker", Title = $"₹{w.Amount:N0} water tanker payment",
                Subtitle = "Flat " + w.Flat.FlatNumber, Timestamp = w.PaymentDate!.Value
            })
            .ToListAsync(ct);

        return payments.Concat(complaints).Concat(visits).Concat(waterTanker)
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .ToList();
    }
}

/// <summary>Member dashboard summary — same "wire up what exists today" approach.</summary>
public class MemberDashboardSummaryDto
{
    public decimal MyMaintenanceDue { get; set; }
    public int UnreadNoticesCount { get; set; } // TODO(Notice Board module)
    public int UpcomingEventsCount { get; set; }
    public int MyOpenComplaintsCount { get; set; }
}

public record GetMemberDashboardSummaryQuery : IRequest<MemberDashboardSummaryDto>;

public class GetMemberDashboardSummaryQueryHandler
    : IRequestHandler<GetMemberDashboardSummaryQuery, MemberDashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMemberDashboardSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MemberDashboardSummaryDto> Handle(GetMemberDashboardSummaryQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        // Walks User -> Member -> current FlatResidency -> Flat -> unpaid
        // MaintenanceBills. 0 if the logged-in User has no linked Member yet
        // (e.g. the bootstrap Admin, or a Member not yet given login access).
        decimal myMaintenanceDue = 0;
        var currentFlatId = await _context.Members
            .Where(m => m.UserId == _currentUser.UserId && !m.IsDeleted)
            .SelectMany(m => m.Residencies)
            .Where(r => !r.IsDeleted && r.MoveOutDate == null)
            .Select(r => (int?)r.FlatId)
            .FirstOrDefaultAsync(ct);

        if (currentFlatId.HasValue)
        {
            myMaintenanceDue = await _context.MaintenanceBills
                .Where(b => !b.IsDeleted && !b.IsRolledForward && b.FlatId == currentFlatId && b.Status != BillStatus.Paid)
                .SumAsync(b => (decimal?)(b.TotalAmount - b.AmountPaid), ct) ?? 0;
        }

        return new MemberDashboardSummaryDto
        {
            MyMaintenanceDue = myMaintenanceDue,
            // No SocietyId scoping available yet — matches AdminDashboardSummaryDto's
            // existing system-wide counts.
            UpcomingEventsCount = await _context.Festivals
                .CountAsync(f => !f.IsDeleted && f.Status != FestivalStatus.Completed && f.EndDate >= today, ct),
            // RaisedByUserId is stored directly on Complaint, so this counts
            // everything the caller personally raised — no need to re-derive
            // their current flat(s) the way MyMaintenanceDue does.
            MyOpenComplaintsCount = await _context.Complaints
                .CountAsync(c => !c.IsDeleted && c.RaisedByUserId == _currentUser.UserId && c.Status != ComplaintStatus.Closed, ct)
        };
    }
}
