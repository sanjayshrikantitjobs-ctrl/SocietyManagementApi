using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Tests.Fakes;

public class FakeVehicleOcrService : IVehicleOcrService
{
    public VehicleOcrResult Result { get; set; } = new(true, "MH04AB1234", 0.42, null);

    public Task<VehicleOcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default) =>
        Task.FromResult(Result);
}
