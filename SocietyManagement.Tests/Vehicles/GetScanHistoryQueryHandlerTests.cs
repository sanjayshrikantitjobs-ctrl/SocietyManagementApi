using SocietyManagement.Application.Features.Vehicles;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Infrastructure.Persistence;
using SocietyManagement.Tests.Fakes;
using Xunit;

namespace SocietyManagement.Tests.Vehicles;

public class GetScanHistoryQueryHandlerTests
{
    private static (ApplicationDbContext Db, int SocietyId, int OtherSocietyId, int WatchmanUserId, int AdminUserId) Seed()
    {
        var db = TestDbContextFactory.Create();

        var watchmanRole = new Role { Name = "Watchman", IsSystemRole = true, CreatedBy = "test" };
        var adminRole = new Role { Name = "Admin", IsSystemRole = true, CreatedBy = "test" };
        db.Roles.AddRange(watchmanRole, adminRole);
        db.SaveChanges();

        var society = new Society { Name = "Society A", Address = "a", City = "c", State = "s", Pincode = "1", CreatedBy = "test" };
        var otherSociety = new Society { Name = "Society B", Address = "a", City = "c", State = "s", Pincode = "1", CreatedBy = "test" };
        db.Societies.AddRange(society, otherSociety);
        db.SaveChanges();

        var watchman = new User { FirstName = "Watch", LastName = "Man", Email = "w@test.com", MobileNumber = "1", PasswordHash = "x", RoleId = watchmanRole.Id, SocietyId = society.Id, CreatedBy = "test" };
        var admin = new User { FirstName = "Ad", LastName = "Min", Email = "a@test.com", MobileNumber = "2", PasswordHash = "x", RoleId = adminRole.Id, SocietyId = society.Id, CreatedBy = "test" };
        db.Users.AddRange(watchman, admin);
        db.SaveChanges();

        db.VehicleScanLogs.AddRange(
            new VehicleScanLog { SocietyId = society.Id, ScannedByUserId = watchman.Id, ScannedAt = DateTime.UtcNow, Source = VehicleScanSource.OcrCamera, NormalizedRegistrationNumber = "AAA111", Result = VehicleScanResultStatus.NotRegistered, CreatedBy = "test" },
            new VehicleScanLog { SocietyId = society.Id, ScannedByUserId = admin.Id, ScannedAt = DateTime.UtcNow, Source = VehicleScanSource.ManualSearch, NormalizedRegistrationNumber = "BBB222", Result = VehicleScanResultStatus.Matched, CreatedBy = "test" },
            new VehicleScanLog { SocietyId = otherSociety.Id, ScannedByUserId = watchman.Id, ScannedAt = DateTime.UtcNow, Source = VehicleScanSource.OcrCamera, NormalizedRegistrationNumber = "CCC333", Result = VehicleScanResultStatus.NotRegistered, CreatedBy = "test" }
        );
        db.SaveChanges();

        return (db, society.Id, otherSociety.Id, watchman.Id, admin.Id);
    }

    private static VehicleScanHandlers BuildHandler(ApplicationDbContext db, int societyId, int userId, string roleName)
    {
        var currentUser = new FakeCurrentUserService { UserId = userId, SocietyId = societyId, RoleName = roleName };
        return new VehicleScanHandlers(db, currentUser, new FakeFileStorageService(), new FakeDateTime());
    }

    [Fact]
    public async Task Handle_Watchman_SeesOnlyTheirOwnScans()
    {
        var (db, societyId, _, watchmanUserId, _) = Seed();
        var handler = BuildHandler(db, societyId, watchmanUserId, "Watchman");

        var result = await handler.Handle(new GetScanHistoryQuery(societyId, null, null, null, 1, 10), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("AAA111", result.Items[0].NormalizedRegistrationNumber);
    }

    [Fact]
    public async Task Handle_Admin_SeesTheWholeSocietysScans_IncludingTheWatchmansOwn()
    {
        var (db, societyId, _, _, adminUserId) = Seed();
        var handler = BuildHandler(db, societyId, adminUserId, "Admin");

        var result = await handler.Handle(new GetScanHistoryQuery(societyId, null, null, null, 1, 10), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Handle_NeverReturnsAnotherSocietysScanRows()
    {
        var (db, societyId, otherSocietyId, _, adminUserId) = Seed();
        var handler = BuildHandler(db, societyId, adminUserId, "Admin");

        var result = await handler.Handle(new GetScanHistoryQuery(societyId, null, null, null, 1, 10), CancellationToken.None);

        Assert.DoesNotContain(result.Items, i => i.NormalizedRegistrationNumber == "CCC333");
    }
}
