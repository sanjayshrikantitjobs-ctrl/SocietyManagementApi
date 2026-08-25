using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>An annual (or otherwise recurring) service contract — lift AMC,
/// pest control, water tank cleaning, etc. RenewalDate is the single next
/// due date; on renewal an admin edits it forward rather than this
/// recording a payment/renewal history.</summary>
public class SocietyService : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public string ServiceName { get; set; } = default!;

    public string VendorName { get; set; } = default!;

    public string? ContactPerson { get; set; }

    public string ContactNumber { get; set; } = default!;

    public string? Email { get; set; }

    public DateTime RenewalDate { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
