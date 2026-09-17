using SocietyManagement.Application.Features.Assets;
using Xunit;

namespace SocietyManagement.Tests.Assets;

public class CreateAssetBookingCommandValidatorTests
{
    private readonly CreateAssetBookingCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidSingleItemRequest_IsValid()
    {
        var command = new CreateAssetBookingCommand(1, DateTime.Today, DateTime.Today, null, null,
            new List<AssetBookingItemInput> { new(1, 5) });
        Assert.True(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void Validate_EmptyItemsList_IsInvalid()
    {
        var command = new CreateAssetBookingCommand(1, DateTime.Today, DateTime.Today, null, null, new List<AssetBookingItemInput>());
        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void Validate_ZeroQuantityItem_IsInvalid()
    {
        var command = new CreateAssetBookingCommand(1, DateTime.Today, DateTime.Today, null, null,
            new List<AssetBookingItemInput> { new(1, 0) });
        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void Validate_EndDateBeforeStartDate_IsInvalid()
    {
        var command = new CreateAssetBookingCommand(1, DateTime.Today, DateTime.Today.AddDays(-1), null, null,
            new List<AssetBookingItemInput> { new(1, 1) });
        Assert.False(_validator.Validate(command).IsValid);
    }
}
