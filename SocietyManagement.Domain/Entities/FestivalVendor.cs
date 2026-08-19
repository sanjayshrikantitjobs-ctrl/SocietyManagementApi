using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>Society-scoped vendor directory (Decorator, Sound, Catering, ...),
/// reused across festivals and years — not tied to a single Festival.</summary>
public class FestivalVendor : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string Name { get; set; } = default!;

    public VendorCategory Category { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? GstNumber { get; set; }

    public string? Address { get; set; }

    public decimal Rating { get; set; }

    public ICollection<FestivalExpense> Expenses { get; set; } = new List<FestivalExpense>();
}
