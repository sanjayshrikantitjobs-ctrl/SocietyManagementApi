namespace SocietyManagement.Domain.Enums;

/// <summary>How a VehicleScanLog row was produced — camera + OCR, or a
/// manual search whose result the user opened (logged once, on open, not
/// per keystroke).</summary>
public enum VehicleScanSource
{
    OcrCamera = 1,
    ManualSearch = 2
}

/// <summary>Outcome of matching the scanned/searched registration number
/// against Vehicle.RegistrationNumber within the caller's society. Never
/// drives record creation — NotRegistered is purely informational.</summary>
public enum VehicleScanResultStatus
{
    Matched = 1,
    NotRegistered = 2
}
