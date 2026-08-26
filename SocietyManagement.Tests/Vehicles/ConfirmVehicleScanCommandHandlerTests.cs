using SocietyManagement.Application.Features.Vehicles;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Infrastructure.Persistence;
using SocietyManagement.Tests.Fakes;
using Xunit;
using Permissions = SocietyManagement.Shared.Constants.Permissions;

namespace SocietyManagement.Tests.Vehicles;

public class ConfirmVehicleScanCommandHandlerTests
{
    private static (ApplicationDbContext Db, int SocietyId, int OtherSocietyId, int ScannerUserId) SeedBasicGraph()
    {
        var db = TestDbContextFactory.Create();

        var role = new Role { Name = "Watchman", IsSystemRole = true, CreatedBy = "test" };
        db.Roles.Add(role);
        db.SaveChanges();

        var society = new Society { Name = "Ambesh Tower-1", Address = "a", City = "c", State = "s", Pincode = "1", CreatedBy = "test" };
        var otherSociety = new Society { Name = "Other Society", Address = "a", City = "c", State = "s", Pincode = "1", CreatedBy = "test" };
        db.Societies.AddRange(society, otherSociety);
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

        var member = new Member { SocietyId = society.Id, FirstName = "Sanjay", LastName = "Roy", Phone = "9999999999", Email = "sanjay@test.com", CreatedBy = "test" };
        db.Members.Add(member);
        db.SaveChanges();

        var vehicle = new Vehicle
        {
            MemberId = member.Id, VehicleType = VehicleType.FourWheeler, RegistrationNumber = "MH04AB1234", CreatedBy = "test"
        };
        db.Vehicles.Add(vehicle);
        db.SaveChanges();

        var scanner = new User
        {
            FirstName = "Watch", LastName = "Man", Email = "watch@test.com", MobileNumber = "8888888888",
            PasswordHash = "x", RoleId = role.Id, SocietyId = society.Id, CreatedBy = "test"
        };
        db.Users.Add(scanner);
        db.SaveChanges();

        return (db, society.Id, otherSociety.Id, scanner.Id);
    }

    private static VehicleScanHandlers BuildHandler(
        ApplicationDbContext db, int societyId, int userId, bool canViewOwnerDetails,
        FakeFileStorageService? fileStorage = null)
    {
        var currentUser = new FakeCurrentUserService
        {
            UserId = userId, SocietyId = societyId, RoleName = "Watchman",
            Permissions = canViewOwnerDetails ? new[] { Permissions.Vehicles.ViewOwnerDetails } : Array.Empty<string>()
        };
        return new VehicleScanHandlers(db, currentUser, new FakeVehicleOcrService(), fileStorage ?? new FakeFileStorageService(), new FakeDateTime());
    }

    [Fact]
    public async Task Handle_MatchedVehicle_ReturnsMatchedResultWithoutOwnerDetails_WhenPermissionAbsent()
    {
        var (db, societyId, _, userId) = SeedBasicGraph();
        var handler = BuildHandler(db, societyId, userId, canViewOwnerDetails: false);

        var result = await handler.Handle(
            new ConfirmVehicleScanCommand(societyId, "MH 04 AB 1234", "MH04AB1234", 0.9, VehicleScanSource.OcrCamera, null, null),
            CancellationToken.None);

        Assert.Equal(VehicleScanResultStatus.Matched, result.Result);
        Assert.NotNull(result.VehicleId);
        Assert.Null(result.OwnerName);
        Assert.Null(result.OwnerPhone);
        Assert.Null(result.OwnerEmail);
    }

    [Fact]
    public async Task Handle_MatchedVehicle_IncludesOwnerDetails_WhenPermissionPresent()
    {
        var (db, societyId, _, userId) = SeedBasicGraph();
        var handler = BuildHandler(db, societyId, userId, canViewOwnerDetails: true);

        var result = await handler.Handle(
            new ConfirmVehicleScanCommand(societyId, "MH04AB1234", "MH04AB1234", 0.9, VehicleScanSource.OcrCamera, null, null),
            CancellationToken.None);

        Assert.Equal(VehicleScanResultStatus.Matched, result.Result);
        Assert.Equal("Sanjay Roy", result.OwnerName);
        Assert.Equal("9999999999", result.OwnerPhone);
    }

    [Fact]
    public async Task Handle_UnregisteredPlate_ReturnsNotRegistered_AndCreatesNoVehicle()
    {
        var (db, societyId, _, userId) = SeedBasicGraph();
        var handler = BuildHandler(db, societyId, userId, canViewOwnerDetails: true);
        var vehicleCountBefore = db.Vehicles.Count();

        var result = await handler.Handle(
            new ConfirmVehicleScanCommand(societyId, "KA01ZZ9999", "KA01ZZ9999", 0.3, VehicleScanSource.OcrCamera, null, null),
            CancellationToken.None);

        Assert.Equal(VehicleScanResultStatus.NotRegistered, result.Result);
        Assert.Null(result.VehicleId);
        Assert.Equal(vehicleCountBefore, db.Vehicles.Count());
    }

    [Fact]
    public async Task Handle_PlateRegisteredInAnotherSociety_ReturnsNotRegistered()
    {
        var (db, _, otherSocietyId, userId) = SeedBasicGraph();
        // Vehicle "MH04AB1234" exists, but only in the first society — querying
        // scoped to otherSocietyId must not see it (tenant isolation).
        var handler = BuildHandler(db, otherSocietyId, userId, canViewOwnerDetails: true);

        var result = await handler.Handle(
            new ConfirmVehicleScanCommand(otherSocietyId, "MH04AB1234", "MH04AB1234", 0.9, VehicleScanSource.OcrCamera, null, null),
            CancellationToken.None);

        Assert.Equal(VehicleScanResultStatus.NotRegistered, result.Result);
    }

    [Fact]
    public async Task Handle_EveryConfirmCall_WritesExactlyOneScanLogRow()
    {
        var (db, societyId, _, userId) = SeedBasicGraph();
        var handler = BuildHandler(db, societyId, userId, canViewOwnerDetails: false);
        var before = db.VehicleScanLogs.Count();

        await handler.Handle(
            new ConfirmVehicleScanCommand(societyId, "MH04AB1234", "MH04AB1234", 0.9, VehicleScanSource.OcrCamera, null, null),
            CancellationToken.None);

        Assert.Equal(before + 1, db.VehicleScanLogs.Count());
    }

    [Fact]
    public async Task Handle_ImageBytesProvided_UploadsImageAndStoresUrl()
    {
        var (db, societyId, _, userId) = SeedBasicGraph();
        var fileStorage = new FakeFileStorageService();
        var handler = BuildHandler(db, societyId, userId, canViewOwnerDetails: false, fileStorage);

        await handler.Handle(
            new ConfirmVehicleScanCommand(societyId, "MH04AB1234", "MH04AB1234", 0.9, VehicleScanSource.OcrCamera, null, new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        Assert.Equal(1, fileStorage.SaveCallCount);
        var log = db.VehicleScanLogs.OrderByDescending(l => l.Id).First();
        Assert.NotNull(log.ImageUrl);
    }

    [Fact]
    public async Task Handle_ManualSearchSource_NeverUploadsImage()
    {
        var (db, societyId, _, userId) = SeedBasicGraph();
        var fileStorage = new FakeFileStorageService();
        var handler = BuildHandler(db, societyId, userId, canViewOwnerDetails: false, fileStorage);

        await handler.Handle(
            new ConfirmVehicleScanCommand(societyId, "MH04AB1234", null, null, VehicleScanSource.ManualSearch, null, null),
            CancellationToken.None);

        Assert.Equal(0, fileStorage.SaveCallCount);
    }
}
