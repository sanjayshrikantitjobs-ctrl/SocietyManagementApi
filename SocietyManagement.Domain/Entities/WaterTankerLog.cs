using SocietyManagement.Domain.Common;

namespace SocietyManagement.Domain.Entities;

/// <summary>One tanker delivery — who supplied it, which vehicle, how many
/// tankers, and what it cost. Purely an operational/cost log, not billed to
/// individual flats (see WaterTankerCollection, which this replaces for new
/// entries — that table is left untouched for its existing historical
/// Finance data).</summary>
public class WaterTankerLog : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public DateTime Date { get; set; }
    public string ProviderName { get; set; } = default!;
    public string VehicleNumber { get; set; } = default!;
    public int NumberOfTankers { get; set; }
    public decimal PricePerTanker { get; set; }
    public string? Notes { get; set; }
}
