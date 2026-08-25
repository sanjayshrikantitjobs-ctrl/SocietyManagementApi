using SocietyManagement.Application.Common.Mappings;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Application.Features.Societies.Common;

public class SocietyDto : IMapFrom<Society>
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? RegistrationNumber { get; set; }
    public string Address { get; set; } = default!;
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Pincode { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime? EstablishedDate { get; set; }
    public int BuildingCount { get; set; }
}
