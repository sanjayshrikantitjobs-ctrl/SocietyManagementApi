using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>Attaches to one FlatOccupancy episode (not the eternal Person
/// identity) — Possession Letter, Parking Allotment Letter, Tenant Police
/// NOC and Rental Agreement are all issued in the context of one specific
/// tenancy/ownership episode. Unlimited per occupancy, including multiple
/// "Other" uploads — no uniqueness constraint, unlike RentalAgreement's 1:1
/// relationship (which this generalizes for document display purposes
/// without replacing RentalAgreement's own lease-metadata fields).</summary>
public class ResidentDocument : BaseAuditableEntity
{
    public int FlatOccupancyId { get; set; }
    public FlatOccupancy FlatOccupancy { get; set; } = default!;

    public ResidentDocumentType DocumentType { get; set; }

    public string DocumentUrl { get; set; } = default!;

    public string? Notes { get; set; }

    public int UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = default!;

    public DateTime UploadedAt { get; set; }
}
