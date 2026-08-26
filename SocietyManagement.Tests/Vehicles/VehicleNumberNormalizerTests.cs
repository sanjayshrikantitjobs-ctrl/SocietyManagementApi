using SocietyManagement.Application.Common.Helpers;
using Xunit;

namespace SocietyManagement.Tests.Vehicles;

public class VehicleNumberNormalizerTests
{
    [Theory]
    [InlineData("MH 04 AB 1234", "MH04AB1234")]
    [InlineData("mh04ab1234", "MH04AB1234")]
    [InlineData("MH-04-AB-1234", "MH04AB1234")]
    [InlineData("  MH04AB1234  ", "MH04AB1234")]
    [InlineData("mh 04-ab.1234", "MH04AB1234")]
    public void Normalize_CollapsesEveryCosmeticVariant_ToTheSameCanonicalForm(string raw, string expected)
    {
        Assert.Equal(expected, VehicleNumberNormalizer.Normalize(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrBlankInput_ReturnsEmptyString(string? raw)
    {
        Assert.Equal(string.Empty, VehicleNumberNormalizer.Normalize(raw));
    }
}
