namespace SocietyManagement.Shared.Constants;

/// <summary>The two system roles guaranteed to exist (seeded, non-deletable).
/// Additional custom roles can be created at runtime — see the dynamic
/// Permission system in Constants.Permissions.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Member = "Member";
}

/// <summary>
/// Canonical list of permission codes, grouped by module. Seeded 1:1 into the
/// Permissions table (Database/Seed/02_SeedRolesPermissions.sql) and referenced
/// from [HasPermission("...")] on controller actions, so the compiled code and
/// the DB-driven permission matrix can never silently drift apart.
/// </summary>
public static class Permissions
{
    public static class Members
    {
        public const string View = "members.view";
        public const string Create = "members.create";
        public const string Update = "members.update";
        public const string Delete = "members.delete";
    }

    public static class Society
    {
        public const string View = "society.view";
        public const string Manage = "society.manage"; // buildings/wings/floors/flats/parking CRUD
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
        public const string ManageRoles = "users.manage_roles";
    }

    public static class Roles_
    {
        public const string View = "roles.view";
        public const string Manage = "roles.manage";
    }

    public static class Maintenance
    {
        public const string View = "maintenance.view";
        public const string Manage = "maintenance.manage";
    }

    public static class Festivals
    {
        public const string View = "festivals.view";
        public const string Manage = "festivals.manage"; // festival/budget/sponsor/vendor CRUD
        public const string Contribute = "festivals.contribute"; // member records own donation
        public const string ApproveExpense = "festivals.expense.approve"; // committee approval workflow
    }

    public static class Expenses
    {
        public const string View = "expenses.view";
        public const string Manage = "expenses.manage";
    }

    public static class Notices
    {
        public const string View = "notices.view";
        public const string Manage = "notices.manage";
    }

    public static class Complaints
    {
        public const string View = "complaints.view";
        public const string Create = "complaints.create";
        public const string Manage = "complaints.manage";
    }

    public static class Polls
    {
        public const string View = "polls.view";
        public const string Vote = "polls.vote";
        public const string Manage = "polls.manage";
    }

    public static class Events
    {
        public const string View = "events.view";
        public const string Rsvp = "events.rsvp";
        public const string Manage = "events.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class AuditLogs
    {
        public const string View = "auditlogs.view";
    }
}
