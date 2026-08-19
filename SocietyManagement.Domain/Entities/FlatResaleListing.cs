using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class FlatResaleListing : BaseAuditableEntity
{
    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public int ListedByMemberId { get; set; }
    public Member ListedByMember { get; set; } = default!;

    public decimal AskingPrice { get; set; }

    public DateTime AvailableFrom { get; set; }

    public string? VisitTimingNotes { get; set; }

    public ListingStatus Status { get; set; } = ListingStatus.Available;

    public bool NocRequested { get; set; }

    public DateTime? NocIssuedDate { get; set; }

    public string? NocDocumentUrl { get; set; }

    public string? Notes { get; set; }
}
