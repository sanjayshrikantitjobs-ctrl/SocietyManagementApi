using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Dashboard;

/// <summary>
/// Admin dashboard summary. Only the counters this Foundation phase can actually
/// compute (from Users/Society/Building/Wing/Floor/Flat/ParkingSlot) are wired up;
/// everything that depends on a module not yet built (Maintenance, Complaints,
/// Festivals, Visitors, ...) is present in the DTO at 0/empty with a TODO so the
/// Angular dashboard cards/charts can be laid out now and simply "light up" as
/// each module ships, with no contract changes needed on the frontend.
/// </summary>
public class AdminDashboardSummaryDto
{
    public int TotalMembers { get; set; }
    public int TotalUsers { get; set; }
    public int OccupiedFlats { get; set; }
    public int VacantFlats { get; set; }
    public int TotalFlats { get; set; }
    public int TotalParkingSlots { get; set; }
    public int AllocatedParkingSlots { get; set; }
    public decimal PendingMaintenance { get; set; }
    public decimal CollectedMaintenance { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int UpcomingFestivalsCount { get; set; }
    public int RecentComplaintsCount { get; set; } // TODO(Complaint module)
    public int VisitorsToday { get; set; } // TODO(Visitor Management module)
}

public record GetAdminDashboardSummaryQuery : IRequest<AdminDashboardSummaryDto>;

public class GetAdminDashboardSummaryQueryHandler
    : IRequestHandler<GetAdminDashboardSummaryQuery, AdminDashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDashboardSummaryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminDashboardSummaryDto> Handle(GetAdminDashboardSummaryQuery request, CancellationToken ct)
    {
        var totalFlats = await _context.Flats.CountAsync(f => !f.IsDeleted, ct);
        var occupiedFlats = await _context.Flats.CountAsync(f => !f.IsDeleted && f.Status == FlatStatus.Occupied, ct);
        var today = DateTime.UtcNow.Date;

        var unpaidBills = await _context.MaintenanceBills
            .Where(b => !b.IsDeleted && b.Status != BillStatus.Paid)
            .Select(b => new { b.DueDate, Balance = b.TotalAmount - b.AmountPaid })
            .ToListAsync(ct);

        return new AdminDashboardSummaryDto
        {
            TotalMembers = await _context.Members.CountAsync(m => !m.IsDeleted, ct),
            TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted, ct),
            TotalFlats = totalFlats,
            OccupiedFlats = occupiedFlats,
            VacantFlats = totalFlats - occupiedFlats,
            TotalParkingSlots = await _context.ParkingSlots.CountAsync(p => !p.IsDeleted, ct),
            AllocatedParkingSlots = await _context.ParkingSlots
                .CountAsync(p => !p.IsDeleted && p.Status == ParkingStatus.Allocated, ct),
            // Pending = not yet overdue; Outstanding = every unpaid balance
            // including overdue — two different cuts of the same unpaid-bills set.
            PendingMaintenance = unpaidBills.Where(b => b.DueDate >= today).Sum(b => b.Balance),
            OutstandingAmount = unpaidBills.Sum(b => b.Balance),
            CollectedMaintenance = await _context.MaintenancePayments.Where(p => !p.IsDeleted).SumAsync(p => (decimal?)p.Amount, ct) ?? 0,
            UpcomingFestivalsCount = await _context.Festivals
                .CountAsync(f => !f.IsDeleted && f.Status != FestivalStatus.Completed && f.EndDate >= today, ct)
        };
    }
}

/// <summary>Member dashboard summary — same "wire up what exists today" approach.</summary>
public class MemberDashboardSummaryDto
{
    public decimal MyMaintenanceDue { get; set; }
    public int UnreadNoticesCount { get; set; } // TODO(Notice Board module)
    public int UpcomingEventsCount { get; set; }
    public int MyOpenComplaintsCount { get; set; } // TODO(Complaint module)
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
                .Where(b => !b.IsDeleted && b.FlatId == currentFlatId && b.Status != BillStatus.Paid)
                .SumAsync(b => (decimal?)(b.TotalAmount - b.AmountPaid), ct) ?? 0;
        }

        return new MemberDashboardSummaryDto
        {
            MyMaintenanceDue = myMaintenanceDue,
            // No SocietyId scoping available yet — matches AdminDashboardSummaryDto's
            // existing system-wide counts.
            UpcomingEventsCount = await _context.Festivals
                .CountAsync(f => !f.IsDeleted && f.Status != FestivalStatus.Completed && f.EndDate >= today, ct)
        };
    }
}
