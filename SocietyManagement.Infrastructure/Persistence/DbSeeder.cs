using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;

namespace SocietyManagement.Infrastructure.Persistence;

/// <summary>
/// Idempotent startup seeder: system roles, the full permission matrix, and one
/// bootstrap Admin account. Mirrors Database/Seed/02_SeedRolesPermissions.sql —
/// use one or the other against a given database, not both blindly. Seeding the
/// admin password here (rather than in raw SQL) guarantees the hash always
/// matches the app's real IPasswordHasher (BCrypt work factor), with no
/// hand-computed hash to keep in sync.
/// </summary>
public class DbSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(ApplicationDbContext context, IPasswordHasher passwordHasher, ILogger<DbSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await _context.Database.MigrateAsync();

        var superAdminRole = await SeedRoleAsync(Roles.SuperAdmin,
            "Platform-wide — creates societies, creates Admins, sees every society's data.", isSystemRole: true);
        var adminRole = await SeedRoleAsync(Roles.Admin, "Full access to every module, scoped to one society.", isSystemRole: true);
        var memberRole = await SeedRoleAsync(Roles.Member,
            "Read-only access; can manage own profile, RSVP, vote, raise complaints.", isSystemRole: true);
        var watchmanRole = await SeedRoleAsync(Roles.Watchman,
            "Gate security — creates visitor requests, checks visitors in and out.", isSystemRole: true);

        var allPermissions = await SeedPermissionsAsync();

        // Super Admin gets everything, including Society.Create. Admin gets
        // everything EXCEPT Society.Create — the one capability reserved
        // for Super Admin (creating new societies); every other permission
        // is identical between the two tiers, since the actual boundary
        // between them is SocietyScopeFilter + User.SocietyId, not a
        // smaller permission grant.
        await SeedRolePermissionsAsync(superAdminRole, allPermissions);
        await SeedRolePermissionsAsync(
            adminRole, allPermissions.Where(p => p.Code != Permissions.Society.Create).ToList());

        var memberPermissionCodes = new[]
        {
            Permissions.Members.View, Permissions.Society.View, Permissions.Maintenance.View,
            Permissions.Festivals.View, Permissions.Festivals.Contribute, Permissions.Expenses.View,
            Permissions.Notices.View, Permissions.Complaints.View, Permissions.Complaints.Create,
            Permissions.Polls.View, Permissions.Polls.Vote, Permissions.Events.View, Permissions.Events.Rsvp,
            Permissions.Visitors.View, Permissions.Visitors.Approve, Permissions.Visitors.Reject,
            Permissions.Occupancy.View, Permissions.Committee.View, Permissions.Occupancy.ManageOwn
        };
        await SeedRolePermissionsAsync(
            memberRole, allPermissions.Where(p => memberPermissionCodes.Contains(p.Code)).ToList());

        var watchmanPermissionCodes = new[]
        {
            Permissions.Society.View, Permissions.Visitors.View, Permissions.Visitors.Create,
            Permissions.Visitors.CheckIn, Permissions.Visitors.CheckOut,
            Permissions.Vehicles.Scan, Permissions.Vehicles.Search,
            Permissions.ParkingFines.View, Permissions.ParkingFines.Create
        };
        await SeedRolePermissionsAsync(
            watchmanRole, allPermissions.Where(p => watchmanPermissionCodes.Contains(p.Code)).ToList());

        await SeedAdminUserAsync(superAdminRole);
        await PromoteExistingAdminsToSuperAdminAsync(adminRole, superAdminRole);
        await BackfillUserSocietyIdAsync();
        await BackfillSocietyCodesAsync();
        await BackfillRentalAgreementDocumentsAsync();

        await _context.SaveChangesAsync();
        _logger.LogInformation("Database seeding completed.");
    }

    /// <summary>Member and Flat Owner/Tenant logins never had SocietyId
    /// populated until now, even though both underlying records
    /// (Member.SocietyId, Person.SocietyId) already carry it — every such
    /// login was silently unscoped (indistinguishable from Super Admin to
    /// SocietyScopeFilter). Backfills from the two source tables; naturally
    /// idempotent since it only ever touches rows still null. Users has no
    /// working reverse MemberId (never actually set anywhere in the
    /// codebase — confirmed by grep), so the Member side must join from
    /// Members, not from Users.</summary>
    private async Task BackfillUserSocietyIdAsync()
    {
        var membersWithLogin = await _context.Members
            .Where(m => m.UserId != null && !m.IsDeleted)
            .Select(m => new { m.UserId, m.SocietyId })
            .ToListAsync();

        var usersById = await _context.Users
            .Where(u => u.SocietyId == null && !u.IsDeleted)
            .ToDictionaryAsync(u => u.Id);

        var backfilledCount = 0;
        foreach (var m in membersWithLogin)
        {
            if (m.UserId.HasValue && usersById.TryGetValue(m.UserId.Value, out var user))
            {
                user.SocietyId = m.SocietyId;
                backfilledCount++;
            }
        }

        var usersWithPerson = await _context.Users
            .Where(u => u.SocietyId == null && u.PersonId != null && !u.IsDeleted)
            .Include(u => u.Person)
            .ToListAsync();
        foreach (var user in usersWithPerson)
        {
            user.SocietyId = user.Person!.SocietyId;
            backfilledCount++;
        }

        if (backfilledCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogWarning("Backfilled SocietyId for {Count} pre-existing Member/Occupancy login(s).", backfilledCount);
        }

        var stillOrphaned = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.SocietyId == null && !u.IsDeleted && u.Role.Name != Roles.SuperAdmin)
            .ToListAsync();
        if (stillOrphaned.Count > 0)
        {
            _logger.LogWarning(
                "{Count} non-SuperAdmin user(s) still have no SocietyId (no Member/Person link to backfill from) " +
                "and need manual reassignment via the Users screen: {Emails}",
                stillOrphaned.Count, string.Join(", ", stillOrphaned.Select(u => u.Email)));
        }
    }

    /// <summary>Every Society created before the Society Code login gate
    /// existed has a null Code — generate one so login enforcement (and
    /// the Admin UI) has something to show/validate against immediately.</summary>
    private async Task BackfillSocietyCodesAsync()
    {
        var uncoded = await _context.Societies.Where(s => s.Code == null && !s.IsDeleted).ToListAsync();
        if (uncoded.Count == 0) return;

        var existingCodes = (await _context.Societies.Where(s => s.Code != null).Select(s => s.Code!).ToListAsync())
            .ToHashSet();

        foreach (var society in uncoded)
        {
            society.Code = GenerateUniqueSocietyCode(existingCodes);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Generated Society codes for {Count} pre-existing society(ies).", uncoded.Count);
    }

    private static string GenerateUniqueSocietyCode(HashSet<string> existingCodes)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do
        {
            var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(6);
            code = new string(random.Select(b => chars[b % chars.Length]).ToArray());
        } while (!existingCodes.Add(code));

        return code;
    }

    /// <summary>ResidentDocument generalizes RentalAgreement's one-off
    /// AgreementDocumentUrl field into a unified per-occupancy Documents
    /// list — this makes every pre-existing agreement's document show up
    /// there too, without touching RentalAgreement's own lease-metadata
    /// fields (dates/deposit/police verification stay right where they
    /// are). Idempotent: only inserts where no RentalAgreement-type
    /// ResidentDocument already exists for that occupancy.</summary>
    private async Task BackfillRentalAgreementDocumentsAsync()
    {
        var alreadyBackfilledOccupancyIds = await _context.ResidentDocuments
            .Where(d => d.DocumentType == ResidentDocumentType.RentalAgreement && !d.IsDeleted)
            .Select(d => d.FlatOccupancyId)
            .ToListAsync();

        var agreementsToBackfill = await _context.RentalAgreements
            .Where(r => !r.IsDeleted && r.AgreementDocumentUrl != null
                && !alreadyBackfilledOccupancyIds.Contains(r.FlatOccupancyId))
            .ToListAsync();
        if (agreementsToBackfill.Count == 0) return;

        var usersByEmail = await _context.Users.ToDictionaryAsync(u => u.Email);
        var fallbackAdmin = await _context.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Role.Name == Roles.SuperAdmin && !u.IsDeleted);

        foreach (var agreement in agreementsToBackfill)
        {
            var uploadedBy = (agreement.CreatedBy != null && usersByEmail.TryGetValue(agreement.CreatedBy, out var user))
                ? user : fallbackAdmin;
            if (uploadedBy == null) continue; // no resolvable user at all — skip, nothing sane to attribute it to.

            await _context.ResidentDocuments.AddAsync(new ResidentDocument
            {
                FlatOccupancyId = agreement.FlatOccupancyId,
                DocumentType = ResidentDocumentType.RentalAgreement,
                DocumentUrl = agreement.AgreementDocumentUrl!,
                UploadedByUserId = uploadedBy.Id,
                UploadedAt = agreement.CreatedAt
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Backfilled {Count} pre-existing rental agreement document(s) into ResidentDocuments.", agreementsToBackfill.Count);
    }

    /// <summary>One-time migration for databases seeded before Super Admin
    /// existed: every user still holding the Admin role with no SocietyId
    /// (impossible for a NEW Admin, who is always created with one via the
    /// Create-Admin-for-Society flow) is a pre-existing admin and gets
    /// promoted to Super Admin — matching "the admin role should be
    /// modified as super admin" directly. Naturally idempotent: after the
    /// first run, zero Admin-role users have a null SocietyId left to
    /// match.</summary>
    private async Task PromoteExistingAdminsToSuperAdminAsync(Role adminRole, Role superAdminRole)
    {
        var legacyAdmins = await _context.Users
            .Where(u => u.RoleId == adminRole.Id && u.SocietyId == null && !u.IsDeleted)
            .ToListAsync();

        if (legacyAdmins.Count == 0) return;

        foreach (var user in legacyAdmins)
        {
            user.RoleId = superAdminRole.Id;
        }

        _logger.LogWarning(
            "Promoted {Count} pre-existing Admin-role user(s) with no SocietyId to Super Admin: {Emails}",
            legacyAdmins.Count, string.Join(", ", legacyAdmins.Select(u => u.Email)));
    }

    private async Task<Role> SeedRoleAsync(string name, string description, bool isSystemRole)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == name);
        if (role is not null) return role;

        role = new Role { Name = name, Description = description, IsSystemRole = isSystemRole, CreatedBy = "system" };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();
        return role;
    }

    private async Task<List<Permission>> SeedPermissionsAsync()
    {
        var seedList = new (string Module, string Action, string Code)[]
        {
            ("Members", "View", Permissions.Members.View),
            ("Members", "Create", Permissions.Members.Create),
            ("Members", "Update", Permissions.Members.Update),
            ("Members", "Delete", Permissions.Members.Delete),
            ("Society", "View", Permissions.Society.View),
            ("Society", "Manage", Permissions.Society.Manage),
            ("Society", "Create", Permissions.Society.Create),
            ("Users", "View", Permissions.Users.View),
            ("Users", "Create", Permissions.Users.Create),
            ("Users", "Update", Permissions.Users.Update),
            ("Users", "Delete", Permissions.Users.Delete),
            ("Users", "ManageRoles", Permissions.Users.ManageRoles),
            ("Roles", "View", Permissions.Roles_.View),
            ("Roles", "Manage", Permissions.Roles_.Manage),
            ("Maintenance", "View", Permissions.Maintenance.View),
            ("Maintenance", "Manage", Permissions.Maintenance.Manage),
            ("Festivals", "View", Permissions.Festivals.View),
            ("Festivals", "Manage", Permissions.Festivals.Manage),
            ("Festivals", "Contribute", Permissions.Festivals.Contribute),
            ("Festivals", "ApproveExpense", Permissions.Festivals.ApproveExpense),
            ("Expenses", "View", Permissions.Expenses.View),
            ("Expenses", "Manage", Permissions.Expenses.Manage),
            ("Notices", "View", Permissions.Notices.View),
            ("Notices", "Manage", Permissions.Notices.Manage),
            ("Complaints", "View", Permissions.Complaints.View),
            ("Complaints", "Create", Permissions.Complaints.Create),
            ("Complaints", "Manage", Permissions.Complaints.Manage),
            ("Polls", "View", Permissions.Polls.View),
            ("Polls", "Vote", Permissions.Polls.Vote),
            ("Polls", "Manage", Permissions.Polls.Manage),
            ("Events", "View", Permissions.Events.View),
            ("Events", "Rsvp", Permissions.Events.Rsvp),
            ("Events", "Manage", Permissions.Events.Manage),
            ("Visitors", "View", Permissions.Visitors.View),
            ("Visitors", "Create", Permissions.Visitors.Create),
            ("Visitors", "Approve", Permissions.Visitors.Approve),
            ("Visitors", "Reject", Permissions.Visitors.Reject),
            ("Visitors", "CheckIn", Permissions.Visitors.CheckIn),
            ("Visitors", "CheckOut", Permissions.Visitors.CheckOut),
            ("Visitors", "Manage", Permissions.Visitors.Manage),
            ("Visitors", "ManageGates", Permissions.Visitors.ManageGates),
            ("Visitors", "ManagePurposes", Permissions.Visitors.ManagePurposes),
            ("Visitors", "ViewHistory", Permissions.Visitors.ViewHistory),
            ("Visitors", "ManualOverride", Permissions.Visitors.ManualOverride),
            ("Visitors", "ViewReports", Permissions.Visitors.ViewReports),
            ("Visitors", "ManageFrequentVisitors", Permissions.Visitors.ManageFrequentVisitors),
            ("Visitors", "ManageDomesticHelp", Permissions.Visitors.ManageDomesticHelp),
            ("Visitors", "ManageExpectedVisitors", Permissions.Visitors.ManageExpectedVisitors),
            ("Visitors", "ScanQr", Permissions.Visitors.ScanQr),
            ("Reports", "View", Permissions.Reports.View),
            ("AuditLogs", "View", Permissions.AuditLogs.View),
            ("Occupancy", "View", Permissions.Occupancy.View),
            ("Occupancy", "Manage", Permissions.Occupancy.Manage),
            ("Occupancy", "ManageSettings", Permissions.Occupancy.ManageSettings),
            ("Occupancy", "ViewHistory", Permissions.Occupancy.ViewHistory),
            ("Occupancy", "ManageOwn", Permissions.Occupancy.ManageOwn),
            ("Staff", "View", Permissions.Staff.View),
            ("Staff", "Manage", Permissions.Staff.Manage),
            ("Services", "View", Permissions.Services.View),
            ("Services", "Manage", Permissions.Services.Manage),
            ("Committee", "View", Permissions.Committee.View),
            ("Committee", "Manage", Permissions.Committee.Manage),
            ("Vehicles", "Scan", Permissions.Vehicles.Scan),
            ("Vehicles", "Search", Permissions.Vehicles.Search),
            ("Vehicles", "ViewOwnerDetails", Permissions.Vehicles.ViewOwnerDetails),
            ("Vehicles", "Register", Permissions.Vehicles.Register),
            ("ParkingFines", "View", Permissions.ParkingFines.View),
            ("ParkingFines", "Create", Permissions.ParkingFines.Create),
            ("ParkingFines", "Delete", Permissions.ParkingFines.Delete)
        };

        var existingCodes = await _context.Permissions.Select(p => p.Code).ToListAsync();
        var toAdd = seedList.Where(s => !existingCodes.Contains(s.Code))
            .Select(s => new Permission { Module = s.Module, Action = s.Action, Code = s.Code, CreatedBy = "system" })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _context.Permissions.AddRangeAsync(toAdd);
            await _context.SaveChangesAsync();
        }

        return await _context.Permissions.ToListAsync();
    }

    private async Task SeedRolePermissionsAsync(Role role, List<Permission> permissions)
    {
        var existing = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var toAdd = permissions.Where(p => !existing.Contains(p.Id))
            .Select(p => new RolePermission { RoleId = role.Id, PermissionId = p.Id, CreatedBy = "system" })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _context.RolePermissions.AddRangeAsync(toAdd);
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedAdminUserAsync(Role superAdminRole)
    {
        const string adminEmail = "admin@societymanagement.local";

        if (await _context.Users.AnyAsync(u => u.Email == adminEmail))
        {
            return;
        }

        var admin = new User
        {
            FirstName = "System",
            LastName = "Administrator",
            Email = adminEmail,
            MobileNumber = "9999999999",
            PasswordHash = _passwordHasher.Hash("Admin@12345"),
            RoleId = superAdminRole.Id,
            IsActive = true,
            EmailConfirmed = true,
            MobileConfirmed = true,
            MustChangePassword = true, // forces a password change on first login — see spec's security posture
            CreatedBy = "system"
        };

        await _context.Users.AddAsync(admin);
        _logger.LogWarning(
            "Seeded default admin account {Email} with a well-known temporary password. " +
            "MustChangePassword=true forces an immediate reset — change it before exposing this environment.",
            adminEmail);
    }
}
