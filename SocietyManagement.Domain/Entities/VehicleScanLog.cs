using SocietyManagement.Domain.Common;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Domain.Entities;

/// <summary>One scan/search attempt against the Vehicle Security console —
/// a camera+OCR read or an opened manual-search result. Never causes a
/// Vehicle to be created; MatchedVehicleId is null on a NotRegistered
/// result, it's purely a record of what was looked up, by whom, when.</summary>
public class VehicleScanLog : BaseAuditableEntity
{
    public int SocietyId { get; set; }
    public Society Society { get; set; } = default!;

    public int? GateId { get; set; }
    public Gate? Gate { get; set; }

    public int ScannedByUserId { get; set; }
    public User ScannedByUser { get; set; } = default!;

    public DateTime ScannedAt { get; set; }

    public VehicleScanSource Source { get; set; }

    /// <summary>Unedited OCR output, kept for audit even after the user
    /// corrects it — null for ManualSearch entries.</summary>
    public string? RawOcrText { get; set; }

    /// <summary>The confirmed/edited value actually searched with —
    /// normalized via VehicleNumberNormalizer, so it's directly comparable
    /// to Vehicle.RegistrationNumber.</summary>
    public string NormalizedRegistrationNumber { get; set; } = default!;

    /// <summary>OCR confidence 0-1, null for ManualSearch (no OCR involved).</summary>
    public double? Confidence { get; set; }

    /// <summary>Captured plate image, only saved for confirmed OCR scans —
    /// never for a manual search, and never for a discarded/retried OCR
    /// attempt that was never confirmed.</summary>
    public string? ImageUrl { get; set; }

    public int? MatchedVehicleId { get; set; }
    public Vehicle? MatchedVehicle { get; set; }

    public VehicleScanResultStatus Result { get; set; }
}
