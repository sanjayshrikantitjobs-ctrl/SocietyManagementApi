using SocietyManagement.Application.Features.Assets;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Tests.Fakes;
using Xunit;
using Permissions = SocietyManagement.Shared.Constants.Permissions;

namespace SocietyManagement.Tests.Assets;

/// <summary>Covers the CRITICAL "never rent more than the available
/// quantity" guarantee — see AssetBookingFeature.CreateAssetBookingCommandHandler,
/// which sums already-reserved quantity for the overlapping date range and
/// checks it against Asset.TotalQuantity inside one Serializable transaction.
/// Uses SQLite in-memory (not EF Core's InMemory provider) because the
/// handler opens a real transaction — see TestDbContextFactory.CreateSqlite.</summary>
public class CreateAssetBookingCommandHandlerTests
{
    private static (SqliteTestDatabase Db, int FlatId, int AssetId, int UserId) SeedBasicGraph(int totalQuantity = 5)
    {
        var sqlite = TestDbContextFactory.CreateSqlite();
        var db = sqlite.Db;

        var role = new Role { Name = "Admin", IsSystemRole = true, CreatedBy = "test" };
        db.Roles.Add(role);
        db.SaveChanges();

        var society = new Society { Name = "Ambesh Tower-1", Address = "a", City = "c", State = "s", Pincode = "1", CreatedBy = "test" };
        db.Societies.Add(society);
        db.SaveChanges();

        var building = new Building { SocietyId = society.Id, Name = "A", CreatedBy = "test" };
        db.Buildings.Add(building);
        db.SaveChanges();
        var wing = new Wing { BuildingId = building.Id, Name = "Wing 1", CreatedBy = "test" };
        db.Wings.Add(wing);
        db.SaveChanges();
        var floor = new Floor { WingId = wing.Id, FloorNumber = 1, CreatedBy = "test" };
        db.Floors.Add(floor);
        db.SaveChanges();
        var flat = new Flat { FloorId = floor.Id, FlatNumber = "101", FlatType = FlatType.TwoBHK, CreatedBy = "test" };
        db.Flats.Add(flat);
        db.SaveChanges();

        var asset = new Asset
        {
            SocietyId = society.Id, Name = "Plastic Chairs", Category = AssetCategory.Chairs, TotalQuantity = totalQuantity,
            PricingType = AssetPricingType.PerItem, RentalPrice = 10, IsActive = true, CreatedBy = "test"
        };
        db.Assets.Add(asset);
        db.SaveChanges();

        var user = new User
        {
            FirstName = "Sanjay", LastName = "Roy", Email = "sanjay@test.com", MobileNumber = "9999999999",
            PasswordHash = "x", RoleId = role.Id, SocietyId = society.Id, CreatedBy = "test"
        };
        db.Users.Add(user);
        db.SaveChanges();

        return (sqlite, flat.Id, asset.Id, user.Id);
    }

    private static AssetBookingCommandHandlers BuildHandler(SqliteTestDatabase sqlite, int societyId, int userId)
    {
        var currentUser = new FakeCurrentUserService
        {
            UserId = userId, SocietyId = societyId, RoleName = "Admin",
            Permissions = new[] { Permissions.Assets.Manage }
        };
        return new AssetBookingCommandHandlers(sqlite.Db, currentUser, new FakeAuditService());
    }

    [Fact]
    public async Task Handle_BookingUpToTotalQuantity_Succeeds()
    {
        var (sqlite, flatId, assetId, userId) = SeedBasicGraph(totalQuantity: 5);
        using var _ = sqlite;
        var societyId = sqlite.Db.Assets.First().SocietyId;
        var handler = BuildHandler(sqlite, societyId, userId);
        var start = DateTime.UtcNow.Date.AddDays(1);
        var end = start.AddDays(2);

        var id = await handler.Handle(
            new CreateAssetBookingCommand(flatId, start, end, null, null, new List<AssetBookingItemInput> { new(assetId, 5) }),
            CancellationToken.None);

        Assert.True(id > 0);
        Assert.Equal(AssetBookingStatus.Pending, sqlite.Db.AssetBookings.Single(b => b.Id == id).Status);
    }

    [Fact]
    public async Task Handle_RequestExceedingRemainingQuantityOnOverlappingDates_ThrowsConflict()
    {
        var (sqlite, flatId, assetId, userId) = SeedBasicGraph(totalQuantity: 5);
        using var _ = sqlite;
        var societyId = sqlite.Db.Assets.First().SocietyId;
        var handler = BuildHandler(sqlite, societyId, userId);
        var start = DateTime.UtcNow.Date.AddDays(1);
        var end = start.AddDays(2);

        await handler.Handle(
            new CreateAssetBookingCommand(flatId, start, end, null, null, new List<AssetBookingItemInput> { new(assetId, 5) }),
            CancellationToken.None);

        // Only 0 remain for any date overlapping [start, end] — requesting even 1 more must fail.
        await Assert.ThrowsAsync<ConflictAppException>(() => handler.Handle(
            new CreateAssetBookingCommand(flatId, start.AddDays(1), end.AddDays(1), null, null, new List<AssetBookingItemInput> { new(assetId, 1) }),
            CancellationToken.None));

        Assert.Single(sqlite.Db.AssetBookings);
    }

    [Fact]
    public async Task Handle_RequestForNonOverlappingDateRange_SucceedsEvenWhenFullyBookedElsewhere()
    {
        var (sqlite, flatId, assetId, userId) = SeedBasicGraph(totalQuantity: 5);
        using var _ = sqlite;
        var societyId = sqlite.Db.Assets.First().SocietyId;
        var handler = BuildHandler(sqlite, societyId, userId);
        var start = DateTime.UtcNow.Date.AddDays(1);
        var end = start.AddDays(2);

        await handler.Handle(
            new CreateAssetBookingCommand(flatId, start, end, null, null, new List<AssetBookingItemInput> { new(assetId, 5) }),
            CancellationToken.None);

        // A date range that starts only after the first booking's end date — no overlap.
        var secondId = await handler.Handle(
            new CreateAssetBookingCommand(flatId, end.AddDays(1), end.AddDays(3), null, null, new List<AssetBookingItemInput> { new(assetId, 5) }),
            CancellationToken.None);

        Assert.True(secondId > 0);
        Assert.Equal(2, sqlite.Db.AssetBookings.Count());
    }

    [Fact]
    public async Task Handle_MultipleAssetsInOneRequest_ComputesTotalAcrossAllLines()
    {
        var (sqlite, flatId, assetId, userId) = SeedBasicGraph(totalQuantity: 5);
        using var _ = sqlite;
        var db = sqlite.Db;
        var speaker = new Asset
        {
            SocietyId = db.Assets.First().SocietyId, Name = "Speaker", Category = AssetCategory.Speakers, TotalQuantity = 3,
            PricingType = AssetPricingType.PerItem, RentalPrice = 1000, IsActive = true, CreatedBy = "test"
        };
        db.Assets.Add(speaker);
        db.SaveChanges();

        var handler = BuildHandler(sqlite, db.Assets.First().SocietyId, userId);
        var start = DateTime.UtcNow.Date.AddDays(1);
        var end = start.AddDays(1);

        var id = await handler.Handle(
            new CreateAssetBookingCommand(flatId, start, end, null, null,
                new List<AssetBookingItemInput> { new(assetId, 4), new(speaker.Id, 2) }),
            CancellationToken.None);

        var booking = db.AssetBookings.Single(b => b.Id == id);
        // 4 chairs * 10 + 2 speakers * 1000 = 2040.
        Assert.Equal(2040, booking.RentalCharge);
    }
}
