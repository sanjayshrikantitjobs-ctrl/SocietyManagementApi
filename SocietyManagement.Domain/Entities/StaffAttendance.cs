using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One staff member's attendance for one calendar day — a unique
/// (StaffId, Date) row, upserted by the mark-attendance command.</summary>
public class StaffAttendance : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int StaffId { get; set; }
    public Staff Staff { get; set; } = default!;

    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    public string? Notes { get; set; }
}
