using SocietyManagement.Application.Features.Facilities;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Tests.Fakes;
using Xunit;
using Permissions = SocietyManagement.Shared.Constants.Permissions;

namespace SocietyManagement.Tests.Facilities;

/// <summary>Covers the CRITICAL "never double-book the same facility/time
/// slot" guarantee — see FacilityBookingFeature.CreateFacilityBookingCommandHandler,
/// which runs the overlap check and the insert inside one Serializable
/// transaction. EF Core's InMemory provider can't open a real transaction
/// (Database.BeginTransactionAsync is relational-only), so these tests use
/// a SQLite in-memory database instead — see TestDbContextFactory.CreateSqlite.</summary>
public class CreateFacilityBookingCommandHandlerTests
{
    private static (SqliteTestDatabase Db, int FlatId, int FacilityId, int UserId) SeedBasicGraph(bool requiresApproval = false)
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

        var facility = new Facility
        {
            SocietyId = society.Id, Name = "Community Hall", Type = FacilityType.CommunityHall, Capacity = 100,
            PricingType = FacilityPricingType.PerHour, PricePerUnit = 500, RequiresApproval = requiresApproval,
            IsActive = true, CreatedBy = "test"
        };
        db.Facilities.Add(facility);
        db.SaveChanges();

        var user = new User
        {
            FirstName = "Sanjay", LastName = "Roy", Email = "sanjay@test.com", MobileNumber = "9999999999",
            PasswordHash = "x", RoleId = role.Id, SocietyId = society.Id, CreatedBy = "test"
        };
        db.Users.Add(user);
        db.SaveChanges();

        return (sqlite, flat.Id, facility.Id, user.Id);
    }

    private static FacilityBookingCommandHandlers BuildHandler(SqliteTestDatabase sqlite, int societyId, int userId)
    {
        var currentUser = new FakeCurrentUserService
        {
            UserId = userId, SocietyId = societyId, RoleName = "Admin",
            Permissions = new[] { Permissions.Facilities.Manage }
        };
        return new FacilityBookingCommandHandlers(sqlite.Db, currentUser, new FakeAuditService());
    }

    [Fact]
    public async Task Handle_FirstBookingForASlot_Succeeds()
    {
        var (sqlite, flatId, facilityId, userId) = SeedBasicGraph();
        using var _ = sqlite;
        var handler = BuildHandler(sqlite, sqlite.Db.Facilities.First().SocietyId, userId);

        var id = await handler.Handle(
            new CreateFacilityBookingCommand(facilityId, flatId, DateTime.UtcNow.Date.AddDays(1), new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0), "Birthday", 20, null),
            CancellationToken.None);

        Assert.True(id > 0);
        var booking = sqlite.Db.FacilityBookings.Single(b => b.Id == id);
        Assert.Equal(FacilityBookingStatus.Approved, booking.Status);
    }

    [Fact]
    public async Task Handle_OverlappingTimeSlotOnSameDate_ThrowsConflict()
    {
        var (sqlite, flatId, facilityId, userId) = SeedBasicGraph();
        using var _ = sqlite;
        var societyId = sqlite.Db.Facilities.First().SocietyId;
        var handler = BuildHandler(sqlite, societyId, userId);
        var bookingDate = DateTime.UtcNow.Date.AddDays(1);

        await handler.Handle(
            new CreateFacilityBookingCommand(facilityId, flatId, bookingDate, new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0), "Birthday", 20, null),
            CancellationToken.None);

        // 12:00-15:00 overlaps the existing 10:00-13:00 booking (12:00 < 13:00 && 15:00 > 10:00).
        await Assert.ThrowsAsync<ConflictAppException>(() => handler.Handle(
            new CreateFacilityBookingCommand(facilityId, flatId, bookingDate, new TimeSpan(12, 0, 0), new TimeSpan(15, 0, 0), "Anniversary", 10, null),
            CancellationToken.None));

        Assert.Single(sqlite.Db.FacilityBookings);
    }

    [Fact]
    public async Task Handle_NonOverlappingTimeSlotOnSameDate_Succeeds()
    {
        var (sqlite, flatId, facilityId, userId) = SeedBasicGraph();
        using var _ = sqlite;
        var societyId = sqlite.Db.Facilities.First().SocietyId;
        var handler = BuildHandler(sqlite, societyId, userId);
        var bookingDate = DateTime.UtcNow.Date.AddDays(1);

        await handler.Handle(
            new CreateFacilityBookingCommand(facilityId, flatId, bookingDate, new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0), "Birthday", 20, null),
            CancellationToken.None);

        // 13:00-15:00 starts exactly when the first booking ends — no overlap.
        var secondId = await handler.Handle(
            new CreateFacilityBookingCommand(facilityId, flatId, bookingDate, new TimeSpan(13, 0, 0), new TimeSpan(15, 0, 0), "Anniversary", 10, null),
            CancellationToken.None);

        Assert.True(secondId > 0);
        Assert.Equal(2, sqlite.Db.FacilityBookings.Count());
    }

    [Fact]
    public async Task Handle_RequiresApprovalFacility_StartsAsPending()
    {
        var (sqlite, flatId, facilityId, userId) = SeedBasicGraph(requiresApproval: true);
        using var _ = sqlite;
        var societyId = sqlite.Db.Facilities.First().SocietyId;
        var handler = BuildHandler(sqlite, societyId, userId);

        var id = await handler.Handle(
            new CreateFacilityBookingCommand(facilityId, flatId, DateTime.UtcNow.Date.AddDays(1), new TimeSpan(10, 0, 0), new TimeSpan(13, 0, 0), null, 0, null),
            CancellationToken.None);

        Assert.Equal(FacilityBookingStatus.Pending, sqlite.Db.FacilityBookings.Single(b => b.Id == id).Status);
    }
}
