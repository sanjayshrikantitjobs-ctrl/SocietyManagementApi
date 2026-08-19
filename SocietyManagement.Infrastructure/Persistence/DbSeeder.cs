using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
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

        var adminRole = await SeedRoleAsync(Roles.Admin, "Full access to every module.", isSystemRole: true);
        var memberRole = await SeedRoleAsync(Roles.Member,
            "Read-only access; can manage own profile, RSVP, vote, raise complaints.", isSystemRole: true);

        var allPermissions = await SeedPermissionsAsync();

        await SeedRolePermissionsAsync(adminRole, allPermissions);

        var memberPermissionCodes = new[]
        {
            Permissions.Members.View, Permissions.Society.View, Permissions.Maintenance.View,
            Permissions.Festivals.View, Permissions.Festivals.Contribute, Permissions.Expenses.View,
            Permissions.Notices.View, Permissions.Complaints.View, Permissions.Complaints.Create,
            Permissions.Polls.View, Permissions.Polls.Vote, Permissions.Events.View, Permissions.Events.Rsvp
        };
        await SeedRolePermissionsAsync(
            memberRole, allPermissions.Where(p => memberPermissionCodes.Contains(p.Code)).ToList());

        await SeedAdminUserAsync(adminRole);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Database seeding completed.");
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
            ("Reports", "View", Permissions.Reports.View),
            ("AuditLogs", "View", Permissions.AuditLogs.View)
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

    private async Task SeedAdminUserAsync(Role adminRole)
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
            RoleId = adminRole.Id,
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
