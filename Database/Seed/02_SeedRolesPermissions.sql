/* =============================================================================
   Seeds the two system roles and the full permission matrix.
   Idempotent — safe to re-run (uses IF NOT EXISTS guards).
   Keep this list in sync with SocietyManagement.Shared.Constants.Permissions;
   Infrastructure.Persistence.DbSeeder performs the same seed in C# at
   application startup, so either this script OR the app's automatic seeding
   is sufficient — do not run both against a database that already has data
   without checking for duplicates first.
   ============================================================================= */

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Admin')
    INSERT INTO dbo.Roles (Name, Description, IsSystemRole, CreatedBy) VALUES ('Admin', 'Full access to every module.', 1, 'system');

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Member')
    INSERT INTO dbo.Roles (Name, Description, IsSystemRole, CreatedBy) VALUES ('Member', 'Read-only access; can manage own profile, RSVP, vote, raise complaints.', 1, 'system');

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Watchman')
    INSERT INTO dbo.Roles (Name, Description, IsSystemRole, CreatedBy) VALUES ('Watchman', 'Gate security — creates visitor requests, checks visitors in and out.', 1, 'system');
GO

;WITH PermissionSeed AS (
    SELECT * FROM (VALUES
        ('Members',    'View',        'members.view'),
        ('Members',    'Create',      'members.create'),
        ('Members',    'Update',      'members.update'),
        ('Members',    'Delete',      'members.delete'),
        ('Society',    'View',        'society.view'),
        ('Society',    'Manage',      'society.manage'),
        ('Users',      'View',        'users.view'),
        ('Users',      'Create',      'users.create'),
        ('Users',      'Update',      'users.update'),
        ('Users',      'Delete',      'users.delete'),
        ('Users',      'ManageRoles', 'users.manage_roles'),
        ('Roles',      'View',        'roles.view'),
        ('Roles',      'Manage',      'roles.manage'),
        ('Maintenance','View',        'maintenance.view'),
        ('Maintenance','Manage',      'maintenance.manage'),
        ('Festivals',  'View',        'festivals.view'),
        ('Festivals',  'Manage',      'festivals.manage'),
        ('Festivals',  'Contribute',  'festivals.contribute'),
        ('Festivals',  'ApproveExpense', 'festivals.expense.approve'),
        ('Expenses',   'View',        'expenses.view'),
        ('Expenses',   'Manage',      'expenses.manage'),
        ('Notices',    'View',        'notices.view'),
        ('Notices',    'Manage',      'notices.manage'),
        ('Complaints', 'View',        'complaints.view'),
        ('Complaints', 'Create',      'complaints.create'),
        ('Complaints', 'Manage',      'complaints.manage'),
        ('Polls',      'View',        'polls.view'),
        ('Polls',      'Vote',        'polls.vote'),
        ('Polls',      'Manage',      'polls.manage'),
        ('Events',     'View',        'events.view'),
        ('Events',     'Rsvp',        'events.rsvp'),
        ('Events',     'Manage',      'events.manage'),
        ('Visitors',   'View',        'visitors.view'),
        ('Visitors',   'Create',      'visitors.create'),
        ('Visitors',   'Approve',     'visitors.approve'),
        ('Visitors',   'Reject',      'visitors.reject'),
        ('Visitors',   'CheckIn',     'visitors.checkin'),
        ('Visitors',   'CheckOut',    'visitors.checkout'),
        ('Visitors',   'Manage',      'visitors.manage'),
        ('Visitors',   'ManageGates', 'visitors.manage_gates'),
        ('Visitors',   'ManagePurposes', 'visitors.manage_purposes'),
        ('Visitors',   'ViewHistory', 'visitors.view_history'),
        ('Visitors',   'ManualOverride', 'visitors.manual_override'),
        ('Visitors',   'ViewReports', 'visitors.view_reports'),
        ('Visitors',   'ManageFrequentVisitors', 'visitors.manage_frequent'),
        ('Visitors',   'ManageDomesticHelp', 'visitors.manage_domestic_help'),
        ('Visitors',   'ManageExpectedVisitors', 'visitors.manage_expected'),
        ('Visitors',   'ScanQr',      'visitors.scan_qr'),
        ('Reports',    'View',        'reports.view'),
        ('AuditLogs',  'View',        'auditlogs.view')
    ) AS p(Module, Action, Code)
)
INSERT INTO dbo.Permissions (Module, Action, Code, CreatedBy)
SELECT Module, Action, Code, 'system'
FROM PermissionSeed
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions existing WHERE existing.Code = PermissionSeed.Code);
GO

-- Admin: every permission.
INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedBy)
SELECT r.Id, p.Id, 'system'
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.Name = 'Admin'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
  );
GO

-- Member: the read/self-service subset from the spec's permission matrix
-- (Members/Expenses/Notices: View; Festivals: View+Contribute; Complaints: Create+View;
-- Polls: Vote+View; Events: Rsvp+View; Society: View).
INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedBy)
SELECT r.Id, p.Id, 'system'
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.Name = 'Member'
  AND p.Code IN (
      'members.view', 'society.view', 'maintenance.view', 'festivals.view', 'festivals.contribute',
      'expenses.view', 'notices.view', 'complaints.view', 'complaints.create', 'polls.view', 'polls.vote',
      'events.view', 'events.rsvp', 'visitors.view', 'visitors.approve', 'visitors.reject'
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
  );
GO

-- Watchman: create/view visitor requests and check people in/out at the gate.
INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedBy)
SELECT r.Id, p.Id, 'system'
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.Name = 'Watchman'
  AND p.Code IN ('society.view', 'visitors.view', 'visitors.create', 'visitors.checkin', 'visitors.checkout')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id
  );
GO

PRINT 'Roles and permissions seeded successfully.';
