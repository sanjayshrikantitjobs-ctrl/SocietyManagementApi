using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>Optional 1:1 child of a Tenant-typed FlatOccupancy — the
/// Rental Information Card. A lease renewal is modeled as closing the old
/// Tenant FlatOccupancy and opening a new one (with its own new
/// RentalAgreement), consistent with the "close and open new, never
/// delete" history rule — not as editing agreement dates in place.</summary>
public class RentalAgreement : BaseAuditableEntity
{
    public int FlatOccupancyId { get; set; }
    public FlatOccupancy FlatOccupancy { get; set; } = default!;

    public DateTime AgreementStartDate { get; set; }

    public DateTime AgreementEndDate { get; set; }

    public decimal SecurityDeposit { get; set; }

    public decimal? RentAmount { get; set; }

    public PoliceVerificationStatus PoliceVerificationStatus { get; set; } = PoliceVerificationStatus.Pending;

    public string? PoliceVerificationReference { get; set; }

    public string? AgreementDocumentUrl { get; set; }
}
