using SocietyManagement.Application.Features.Facilities;
using Xunit;

namespace SocietyManagement.Tests.Facilities;

public class CreateFacilityBookingCommandValidatorTests
{
    private readonly CreateFacilityBookingCommandValidator _validator = new();

    [Fact]
    public void Validate_EndTimeAfterStartTime_IsValid()
    {
        var command = new CreateFacilityBookingCommand(1, 1, DateTime.Today, new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0), null, 0, null);
        Assert.True(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void Validate_EndTimeBeforeStartTime_IsInvalid()
    {
        var command = new CreateFacilityBookingCommand(1, 1, DateTime.Today, new TimeSpan(13, 0, 0), new TimeSpan(10, 0, 0), null, 0, null);
        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void Validate_EndTimeEqualsStartTime_IsInvalid()
    {
        var command = new CreateFacilityBookingCommand(1, 1, DateTime.Today, new TimeSpan(10, 0, 0), new TimeSpan(10, 0, 0), null, 0, null);
        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void Validate_ZeroFacilityId_IsInvalid()
    {
        var command = new CreateFacilityBookingCommand(0, 1, DateTime.Today, new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0), null, 0, null);
        Assert.False(_validator.Validate(command).IsValid);
    }
}
