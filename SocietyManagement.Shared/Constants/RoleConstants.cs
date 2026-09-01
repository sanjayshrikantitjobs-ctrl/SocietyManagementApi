namespace SocietyManagement.Shared.Constants;

/// <summary>The two system roles guaranteed to exist (seeded, non-deletable).
/// Additional custom roles can be created at runtime — see the dynamic
/// Permission system in Constants.Permissions.</summary>
public static class Roles
{
    /// <summary>Platform-wide — no SocietyId, sees/manages every society,
    /// the only role that can create a Society or another Admin.</summary>
    public const string SuperAdmin = "SuperAdmin";
    /// <summary>Scoped to exactly one Society via User.SocietyId — same
    /// permission set as SuperAdmin (see Permissions.Society.Create for the
    /// one exception), enforced by SocietyScopeFilter rather than a smaller
    /// permission grant.</summary>
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
        public const string Manage = "society.manage"; // buildings/wings/floors/flats/parking CRUD, own society only
        /// <summary>Create a brand-new Society. Super Admin only — never
        /// granted to the Admin role.</summary>
        public const string Create = "society.create";
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

    /// <summary>The Owner/Tenant Occupancy module (Person/FlatOccupancy/
    /// OccupancyMember/RentalAgreement) — a parallel model to Members.*,
    /// deliberately its own permission group rather than reusing Members.*.</summary>
    public static class Occupancy
    {
        public const string View = "occupancy.view";
        public const string Manage = "occupancy.manage";
        public const string ManageSettings = "occupancy.manage_settings";
        public const string ViewHistory = "occupancy.view_history";
        /// <summary>Self-service: a resident may add a family member to
        /// their own flat, but the handler resolves "own flat" itself from
        /// the caller's identity — never a blanket grant to manage any
        /// flat's occupancy the way Manage is.</summary>
        public const string ManageOwn = "occupancy.manage_own";
    }

    public static class Staff
    {
        public const string View = "staff.view";
        public const string Manage = "staff.manage";
    }

    public static class Services
    {
        public const string View = "services.view";
        public const string Manage = "services.manage";
    }

    public static class Committee
    {
        public const string View = "committee.view";
        public const string Manage = "committee.manage";
    }

    /// <summary>Vehicle Security console (camera OCR + manual search +
    /// scan history) — deliberately its own group, not Members.*, so
    /// Watchman can reach Scan/Search without the broader Members grant.
    /// The existing Vehicle CRUD in VehiclesController keeps using
    /// Members.* unchanged; Register is enforced as a frontend visibility
    /// gate only (see VehicleScanFeature.cs doc comment).</summary>
    public static class Vehicles
    {
        public const string Scan = "vehicles.scan";
        public const string Search = "vehicles.search";
        public const string ViewOwnerDetails = "vehicles.view_owner_details";
        public const string Register = "vehicles.register";
    }

    /// <summary>Own group rather than folded into Vehicles.* — Watchman gets
    /// View/Create here but deliberately never Delete (only Admin/SuperAdmin
    /// can remove a fine).</summary>
    public static class ParkingFines
    {
        public const string View = "parking_fines.view";
        public const string Create = "parking_fines.create";
        public const string Delete = "parking_fines.delete";
    }
}
