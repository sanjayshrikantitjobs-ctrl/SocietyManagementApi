using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

public class FestivalSponsor : BaseAuditableEntity
{
    public int FestivalId { get; set; }
    public Festival Festival { get; set; } = default!;

    public string CompanyName { get; set; } = default!;

    public string? ContactPerson { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public SponsorshipType SponsorshipType { get; set; }

    public decimal PromisedAmount { get; set; }

    public decimal ReceivedAmount { get; set; }

    public string? LogoUrl { get; set; }

    public string? BannerUrl { get; set; }
}
