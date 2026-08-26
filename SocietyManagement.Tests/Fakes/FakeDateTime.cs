using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Tests.Fakes;

public class FakeDateTime : IDateTime
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}
