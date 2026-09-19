using SocietyManagement.Application.Features.Attendance;
using SocietyManagement.Application.Features.SocietyDocuments;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Tests.Fakes;
using Xunit;
using Permissions = SocietyManagement.Shared.Constants.Permissions;

namespace SocietyManagement.Tests.Operations;

public class OperationsHandlerTests
{
    private static Society NewSociety(string name) =>
        new() { Name = name, Address = "a", City = "c", State = "s", Pincode = "1", CreatedBy = "test" };

    [Fact]
    public async Task Documents_UserWithoutManagePermission_OnlySeesAllResidentsDocuments()
    {
        using var db = TestDbContextFactory.Create();
        var society = NewSociety("S1");
        db.Societies.Add(society);
        db.SaveChanges();
        db.SocietyDocuments.AddRange(
            new SocietyDocument { SocietyId = society.Id, Title = "Public", FileUrl = "/a", Visibility = DocumentVisibility.AllResidents, CreatedBy = "t" },
            new SocietyDocument { SocietyId = society.Id, Title = "Admin only", FileUrl = "/b", Visibility = DocumentVisibility.AdminOnly, CreatedBy = "t" });
        db.SaveChanges();

        var resident = new FakeCurrentUserService { SocietyId = society.Id, Permissions = new[] { Permissions.Documents.View } };
        var admin = new FakeCurrentUserService { SocietyId = society.Id, Permissions = new[] { Permissions.Documents.View, Permissions.Documents.Manage } };
        var query = new GetSocietyDocumentsQuery(society.Id, null, null, false);

        var residentView = await new SocietyDocumentHandlers(db, new FakeAuditService(), resident).Handle(query, CancellationToken.None);
        var adminView = await new SocietyDocumentHandlers(db, new FakeAuditService(), admin).Handle(query, CancellationToken.None);

        Assert.Equal(new[] { "Public" }, residentView.Items.Select(d => d.Title));
        Assert.Equal(2, adminView.Items.Count);
    }

    [Fact]
    public async Task Documents_UpdatingAnotherSocietysDocument_IsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var s1 = NewSociety("S1");
        var s2 = NewSociety("S2");
        db.Societies.AddRange(s1, s2);
        db.SaveChanges();
        var doc = new SocietyDocument { SocietyId = s2.Id, Title = "Theirs", FileUrl = "/x", CreatedBy = "t" };
        db.SocietyDocuments.Add(doc);
        db.SaveChanges();

        var caller = new FakeCurrentUserService { SocietyId = s1.Id, Permissions = new[] { Permissions.Documents.Manage } };
        var handler = new SocietyDocumentHandlers(db, new FakeAuditService(), caller);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateSocietyDocumentCommand(doc.Id, "Hijacked", null, SocietyDocumentCategory.Other, "/x", null, null, DocumentVisibility.AllResidents),
            CancellationToken.None));
    }

    [Fact]
    public async Task Attendance_MarkingSameDayTwice_UpdatesTheRowInsteadOfDuplicating()
    {
        using var db = TestDbContextFactory.Create();
        var society = NewSociety("S1");
        db.Societies.Add(society);
        db.SaveChanges();
        var staff = new Staff
        {
            SocietyId = society.Id, FirstName = "A", LastName = "B", Category = StaffCategory.Watchman, Phone = "9999999999",
            JoiningDate = DateTime.UtcNow, SalaryPayDay = 1, CreatedBy = "t"
        };
        db.Staff.Add(staff);
        db.SaveChanges();

        var handler = new StaffAttendanceHandlers(db, new FakeAuditService());
        var day = DateTime.UtcNow.Date;
        var first = await handler.Handle(new MarkAttendanceCommand(society.Id, staff.Id, day, AttendanceStatus.Present, TimeSpan.FromHours(9), null, null), CancellationToken.None);
        var second = await handler.Handle(new MarkAttendanceCommand(society.Id, staff.Id, day, AttendanceStatus.Absent, TimeSpan.FromHours(9), null, null), CancellationToken.None);

        Assert.Equal(first, second);
        var row = Assert.Single(db.StaffAttendances);
        Assert.Equal(AttendanceStatus.Absent, row.Status);
        Assert.Null(row.CheckInTime);
    }

    [Fact]
    public async Task Attendance_StaffFromAnotherSociety_IsNotFound()
    {
        using var db = TestDbContextFactory.Create();
        var s1 = NewSociety("S1");
        var s2 = NewSociety("S2");
        db.Societies.AddRange(s1, s2);
        db.SaveChanges();
        var staff = new Staff
        {
            SocietyId = s2.Id, FirstName = "A", LastName = "B", Category = StaffCategory.Watchman, Phone = "9999999999",
            JoiningDate = DateTime.UtcNow, SalaryPayDay = 1, CreatedBy = "t"
        };
        db.Staff.Add(staff);
        db.SaveChanges();

        var handler = new StaffAttendanceHandlers(db, new FakeAuditService());
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new MarkAttendanceCommand(s1.Id, staff.Id, DateTime.UtcNow.Date, AttendanceStatus.Present, null, null, null), CancellationToken.None));
    }
}
