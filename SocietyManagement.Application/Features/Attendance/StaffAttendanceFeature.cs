using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Attendance;

// One row per active staff member for the chosen day — Status is null when
// nothing has been marked yet.
public class DailyAttendanceDto
{
    public int StaffId { get; set; }
    public string StaffName { get; set; } = default!;
    public StaffCategory Category { get; set; }
    public string? PhotoUrl { get; set; }
    public int? AttendanceId { get; set; }
    public AttendanceStatus? Status { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    public string? Notes { get; set; }
}

public class MonthlyAttendanceRowDto
{
    public int StaffId { get; set; }
    public string StaffName { get; set; } = default!;
    public StaffCategory Category { get; set; }
    public int Present { get; set; }
    public int Late { get; set; }
    public int HalfDay { get; set; }
    public int Absent { get; set; }
    public int Leave { get; set; }
    public int Unmarked { get; set; }
}

public class MonthlyAttendanceDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int DaysInPeriod { get; set; }
    public List<MonthlyAttendanceRowDto> Rows { get; set; } = new();
}

public record MarkAttendanceCommand(
    int SocietyId, int StaffId, DateTime Date, AttendanceStatus Status, TimeSpan? CheckInTime, TimeSpan? CheckOutTime,
    string? Notes) : IRequest<int>;

public class MarkAttendanceCommandValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.StaffId).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.CheckOutTime).GreaterThan(x => x.CheckInTime)
            .When(x => x.CheckInTime.HasValue && x.CheckOutTime.HasValue).WithMessage("Check-out must be after check-in.");
        RuleFor(x => x.Date).Must(d => d.Date <= DateTime.UtcNow.Date.AddDays(1)).WithMessage("Attendance can't be marked for a future date.");
    }
}

public record GetDailyAttendanceQuery(int SocietyId, DateTime Date) : IRequest<List<DailyAttendanceDto>>;

public record GetMonthlyAttendanceQuery(int SocietyId, int Year, int Month) : IRequest<MonthlyAttendanceDto>;

public class StaffAttendanceHandlers :
    IRequestHandler<MarkAttendanceCommand, int>,
    IRequestHandler<GetDailyAttendanceQuery, List<DailyAttendanceDto>>,
    IRequestHandler<GetMonthlyAttendanceQuery, MonthlyAttendanceDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public StaffAttendanceHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    // Upsert on (StaffId, Date): re-marking the same day corrects the row
    // instead of creating a duplicate.
    public async Task<int> Handle(MarkAttendanceCommand r, CancellationToken ct)
    {
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == r.StaffId && s.SocietyId == r.SocietyId, ct)
            ?? throw new NotFoundException("Staff", r.StaffId);

        var date = r.Date.Date;
        var row = await _context.StaffAttendances.FirstOrDefaultAsync(a => a.StaffId == staff.Id && a.Date == date, ct);
        var isNew = row is null;
        row ??= new StaffAttendance { SocietyId = r.SocietyId, StaffId = staff.Id, Date = date };

        row.Status = r.Status;
        // Absent/Leave carry no clock times — drop any stale ones.
        var hasTimes = r.Status is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.HalfDay;
        row.CheckInTime = hasTimes ? r.CheckInTime : null;
        row.CheckOutTime = hasTimes ? r.CheckOutTime : null;
        row.Notes = r.Notes;

        if (isNew) await _context.StaffAttendances.AddAsync(row, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(isNew ? AuditAction.Create : AuditAction.Update, "Staff", nameof(StaffAttendance), row.Id.ToString(),
            newValues: new { staff.Id, date, r.Status }, ct: ct);
        return row.Id;
    }

    public async Task<List<DailyAttendanceDto>> Handle(GetDailyAttendanceQuery r, CancellationToken ct)
    {
        var date = r.Date.Date;
        var staff = await _context.Staff.Where(s => s.SocietyId == r.SocietyId && s.IsActive)
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Category, s.PhotoUrl })
            .ToListAsync(ct);
        var marks = await _context.StaffAttendances.Where(a => a.SocietyId == r.SocietyId && a.Date == date)
            .ToDictionaryAsync(a => a.StaffId, ct);

        return staff.Select(s =>
        {
            marks.TryGetValue(s.Id, out var a);
            return new DailyAttendanceDto
            {
                StaffId = s.Id, StaffName = $"{s.FirstName} {s.LastName}".Trim(), Category = s.Category, PhotoUrl = s.PhotoUrl,
                AttendanceId = a?.Id, Status = a?.Status, CheckInTime = a?.CheckInTime, CheckOutTime = a?.CheckOutTime, Notes = a?.Notes
            };
        }).ToList();
    }

    public async Task<MonthlyAttendanceDto> Handle(GetMonthlyAttendanceQuery r, CancellationToken ct)
    {
        var start = new DateTime(r.Year, r.Month, 1);
        var end = start.AddMonths(1);
        // Days counted against the month so far, so a half-finished month
        // doesn't show every remaining day as "unmarked".
        var today = DateTime.UtcNow.Date;
        var lastCountedDay = today < end ? (today < start ? start.AddDays(-1) : today) : end.AddDays(-1);
        var days = Math.Max((lastCountedDay - start).Days + 1, 0);

        var staff = await _context.Staff.Where(s => s.SocietyId == r.SocietyId && s.IsActive)
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Category })
            .ToListAsync(ct);
        var counts = await _context.StaffAttendances
            .Where(a => a.SocietyId == r.SocietyId && a.Date >= start && a.Date < end)
            .GroupBy(a => new { a.StaffId, a.Status })
            .Select(g => new { g.Key.StaffId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        int Count(int staffId, AttendanceStatus status) => counts.FirstOrDefault(c => c.StaffId == staffId && c.Status == status)?.Count ?? 0;

        var rows = staff.Select(s =>
        {
            var row = new MonthlyAttendanceRowDto
            {
                StaffId = s.Id, StaffName = $"{s.FirstName} {s.LastName}".Trim(), Category = s.Category,
                Present = Count(s.Id, AttendanceStatus.Present), Late = Count(s.Id, AttendanceStatus.Late),
                HalfDay = Count(s.Id, AttendanceStatus.HalfDay), Absent = Count(s.Id, AttendanceStatus.Absent),
                Leave = Count(s.Id, AttendanceStatus.Leave)
            };
            row.Unmarked = Math.Max(days - (row.Present + row.Late + row.HalfDay + row.Absent + row.Leave), 0);
            return row;
        }).ToList();

        return new MonthlyAttendanceDto { Year = r.Year, Month = r.Month, DaysInPeriod = days, Rows = rows };
    }
}
