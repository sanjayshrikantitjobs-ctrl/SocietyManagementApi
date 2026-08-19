namespace SocietyManagement.Shared.Constants;

/// <summary>The two system roles guaranteed to exist (seeded, non-deletable).
/// Additional custom roles can be created at runtime — see the dynamic
/// Permission system in Constants.Permissions.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Member = "Member";
    public const string Watchman = "Watchman";
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

    /// <summary>Phase 1 (core approval workflow) wires View/Create/Approve/Reject/
    /// CheckIn/CheckOut/Manage/ManageGates/ManagePurposes. The rest are seeded now
    /// so later phases (QR passes, frequent visitors, domestic help, delivery,
    /// reports) can pick them up without a fresh migration, same as Events.* was
    /// pre-seeded ahead of the Event RSVP module.</summary>
    public static class Visitors
    {
        public const string View = "visitors.view";
        public const string Create = "visitors.create";
        public const string Approve = "visitors.approve";
        public const string Reject = "visitors.reject";
        public const string CheckIn = "visitors.checkin";
        public const string CheckOut = "visitors.checkout";
        public const string Manage = "visitors.manage";
        public const string ManageGates = "visitors.manage_gates";
        public const string ManagePurposes = "visitors.manage_purposes";
        public const string ViewHistory = "visitors.view_history";
        public const string ManualOverride = "visitors.manual_override";
        public const string ViewReports = "visitors.view_reports";
        public const string ManageFrequentVisitors = "visitors.manage_frequent";
        public const string ManageDomesticHelp = "visitors.manage_domestic_help";
        public const string ManageExpectedVisitors = "visitors.manage_expected";
        public const string ScanQr = "visitors.scan_qr";
    }
}
