using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A society employee — watchman, sweeper, gardener, etc.
/// Deliberately separate from Member/Person (residents) and User (login
/// accounts); staff have no login concept here.</summary>
public class Staff : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public StaffCategory Category { get; set; }

    public string Phone { get; set; } = default!;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public DateTime JoiningDate { get; set; }

    public string? JoiningDocumentUrl { get; set; }

    public string? PhotoUrl { get; set; }

    public decimal Salary { get; set; }

    /// <summary>Day of month salary is paid, 1-31 — not a specific payment
    /// record, just the recurring pay-day.</summary>
    public int SalaryPayDay { get; set; }

    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
