using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>A pet registered to a flat — profile, vaccination and
/// identification details for society records and emergencies.</summary>
public class Pet : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int FlatId { get; set; }
    public Flat Flat { get; set; } = default!;

    public string Name { get; set; } = default!;
    public PetType PetType { get; set; }
    public string? Breed { get; set; }
    public string? PhotoUrl { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Identification { get; set; }
    public DateTime? LastVaccinationDate { get; set; }
    public DateTime? NextVaccinationDue { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
