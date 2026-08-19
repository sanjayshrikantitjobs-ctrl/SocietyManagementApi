/* =============================================================================
   Society Management System — Foundation Schema
   Target: Microsoft SQL Server 2019+
   Covers: Identity & dynamic RBAC, Society Setup hierarchy, Audit Log.
   Every business table carries the mandatory audit columns
   (CreatedAt/CreatedBy/ModifiedAt/ModifiedBy/IsDeleted) per spec.
   Run 01_CreateSchema.sql, then Database/Seed/*.sql in numeric order.
   ============================================================================= */

IF DB_ID('SocietyManagementDb') IS NULL
BEGIN
    PRINT 'Run this script while connected to the target database (create it first via CREATE DATABASE SocietyManagementDb;)';
END
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   Roles / Permissions (dynamic RBAC — spec: "Permissions should be stored in database")
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Roles
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Name          NVARCHAR(50)      NOT NULL,
    Description   NVARCHAR(250)     NULL,
    IsSystemRole  BIT               NOT NULL CONSTRAINT DF_Roles_IsSystemRole DEFAULT (0),
    CreatedAt     DATETIME2         NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy     NVARCHAR(256)     NULL,
    ModifiedAt    DATETIME2         NULL,
    ModifiedBy    NVARCHAR(256)     NULL,
    IsDeleted     BIT               NOT NULL CONSTRAINT DF_Roles_IsDeleted DEFAULT (0),
    DeletedAt     DATETIME2         NULL,
    DeletedBy     NVARCHAR(256)     NULL
);
GO
CREATE UNIQUE INDEX UX_Roles_Name ON dbo.Roles(Name) WHERE IsDeleted = 0;
GO

CREATE TABLE dbo.Permissions
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
    Module        NVARCHAR(50)      NOT NULL,
    Action        NVARCHAR(50)      NOT NULL,
    Code          NVARCHAR(100)     NOT NULL,
    Description   NVARCHAR(250)     NULL,
    CreatedAt     DATETIME2         NOT NULL CONSTRAINT DF_Permissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy     NVARCHAR(256)     NULL,
    ModifiedAt    DATETIME2         NULL,
    ModifiedBy    NVARCHAR(256)     NULL,
    IsDeleted     BIT               NOT NULL CONSTRAINT DF_Permissions_IsDeleted DEFAULT (0),
    DeletedAt     DATETIME2         NULL,
    DeletedBy     NVARCHAR(256)     NULL
);
GO
CREATE UNIQUE INDEX UX_Permissions_Code ON dbo.Permissions(Code);
GO

CREATE TABLE dbo.RolePermissions
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RolePermissions PRIMARY KEY,
    RoleId        INT               NOT NULL,
    PermissionId  INT               NOT NULL,
    CreatedAt     DATETIME2         NOT NULL CONSTRAINT DF_RolePermissions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy     NVARCHAR(256)     NULL,
    ModifiedAt    DATETIME2         NULL,
    ModifiedBy    NVARCHAR(256)     NULL,
    IsDeleted     BIT               NOT NULL CONSTRAINT DF_RolePermissions_IsDeleted DEFAULT (0),
    DeletedAt     DATETIME2         NULL,
    DeletedBy     NVARCHAR(256)     NULL,
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(Id) ON DELETE CASCADE
);
GO
CREATE UNIQUE INDEX UX_RolePermissions_Role_Permission ON dbo.RolePermissions(RoleId, PermissionId);
GO

/* ---------------------------------------------------------------------------
   Users (login accounts) + Refresh Tokens + OTP
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Users
(
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    FirstName           NVARCHAR(100)     NOT NULL,
    LastName            NVARCHAR(100)     NOT NULL,
    Email               NVARCHAR(256)     NOT NULL,
    MobileNumber        NVARCHAR(15)      NOT NULL,
    PasswordHash        NVARCHAR(500)     NOT NULL,
    ProfilePhotoUrl     NVARCHAR(500)     NULL,
    RoleId              INT               NOT NULL,
    MemberId            INT               NULL, -- forward FK to Members table, added by Module 2's migration
    IsActive            BIT               NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    IsLocked            BIT               NOT NULL CONSTRAINT DF_Users_IsLocked DEFAULT (0),
    AccessFailedCount   INT               NOT NULL CONSTRAINT DF_Users_AccessFailedCount DEFAULT (0),
    LockedUntil         DATETIME2         NULL,
    EmailConfirmed      BIT               NOT NULL CONSTRAINT DF_Users_EmailConfirmed DEFAULT (0),
    MobileConfirmed     BIT               NOT NULL CONSTRAINT DF_Users_MobileConfirmed DEFAULT (0),
    LastLoginAt         DATETIME2         NULL,
    LastLoginIp         NVARCHAR(50)      NULL,
    MustChangePassword  BIT               NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT (0),
    CreatedAt           DATETIME2         NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy           NVARCHAR(256)     NULL,
    ModifiedAt          DATETIME2         NULL,
    ModifiedBy          NVARCHAR(256)     NULL,
    IsDeleted           BIT               NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
    DeletedAt           DATETIME2         NULL,
    DeletedBy           NVARCHAR(256)     NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id)
);
GO
CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Users_Mobile ON dbo.Users(MobileNumber) WHERE IsDeleted = 0;
CREATE INDEX IX_Users_RoleId ON dbo.Users(RoleId);
GO

CREATE TABLE dbo.RefreshTokens
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    Token            UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_RefreshTokens_Token DEFAULT (NEWID()),
    UserId           INT               NOT NULL,
    ExpiresAt        DATETIME2         NOT NULL,
    RevokedAt        DATETIME2         NULL,
    ReplacedByToken  UNIQUEIDENTIFIER  NULL,
    CreatedByIp      NVARCHAR(50)      NULL,
    RevokedByIp      NVARCHAR(50)      NULL,
    CreatedAt        DATETIME2         NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy        NVARCHAR(256)     NULL,
    ModifiedAt       DATETIME2         NULL,
    ModifiedBy       NVARCHAR(256)     NULL,
    IsDeleted        BIT               NOT NULL CONSTRAINT DF_RefreshTokens_IsDeleted DEFAULT (0),
    DeletedAt        DATETIME2         NULL,
    DeletedBy        NVARCHAR(256)     NULL,
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
GO
CREATE UNIQUE INDEX UX_RefreshTokens_Token ON dbo.RefreshTokens(Token);
CREATE INDEX IX_RefreshTokens_UserId ON dbo.RefreshTokens(UserId);
GO

CREATE TABLE dbo.OtpVerifications
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OtpVerifications PRIMARY KEY,
    Destination   NVARCHAR(256)     NOT NULL,
    CodeHash      NVARCHAR(500)     NOT NULL,
    Purpose       TINYINT           NOT NULL, -- see Domain.Enums.OtpPurpose
    ExpiresAt     DATETIME2         NOT NULL,
    IsUsed        BIT               NOT NULL CONSTRAINT DF_OtpVerifications_IsUsed DEFAULT (0),
    AttemptCount  INT               NOT NULL CONSTRAINT DF_OtpVerifications_AttemptCount DEFAULT (0),
    UserId        INT               NULL,
    CreatedAt     DATETIME2         NOT NULL CONSTRAINT DF_OtpVerifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy     NVARCHAR(256)     NULL,
    ModifiedAt    DATETIME2         NULL,
    ModifiedBy    NVARCHAR(256)     NULL,
    IsDeleted     BIT               NOT NULL CONSTRAINT DF_OtpVerifications_IsDeleted DEFAULT (0),
    DeletedAt     DATETIME2         NULL,
    DeletedBy     NVARCHAR(256)     NULL,
    CONSTRAINT FK_OtpVerifications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
);
GO
CREATE INDEX IX_OtpVerifications_Destination_Purpose ON dbo.OtpVerifications(Destination, Purpose);
GO

/* ---------------------------------------------------------------------------
   Audit Log (immutable, append-only — no soft-delete columns by design)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.AuditLogs
(
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
    UserId      INT               NULL,
    UserName    NVARCHAR(256)     NULL,
    Action      TINYINT           NOT NULL, -- see Domain.Enums.AuditAction
    Module      NVARCHAR(100)     NOT NULL,
    EntityName  NVARCHAR(100)     NULL,
    EntityId    NVARCHAR(50)      NULL,
    OldValues   NVARCHAR(MAX)     NULL,
    NewValues   NVARCHAR(MAX)     NULL,
    IpAddress   NVARCHAR(50)      NULL,
    UserAgent   NVARCHAR(500)     NULL,
    Timestamp   DATETIME2         NOT NULL CONSTRAINT DF_AuditLogs_Timestamp DEFAULT (SYSUTCDATETIME())
);
GO
CREATE INDEX IX_AuditLogs_Timestamp ON dbo.AuditLogs(Timestamp);
CREATE INDEX IX_AuditLogs_UserId ON dbo.AuditLogs(UserId);
GO

/* ---------------------------------------------------------------------------
   Society Setup: Society -> Building -> Wing -> Floor -> Flat, Parking
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Societies
(
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Societies PRIMARY KEY,
    Name                NVARCHAR(200)     NOT NULL,
    RegistrationNumber  NVARCHAR(100)     NULL,
    Address             NVARCHAR(500)     NOT NULL,
    City                NVARCHAR(100)     NOT NULL,
    State               NVARCHAR(100)     NOT NULL,
    Pincode             NVARCHAR(10)      NOT NULL,
    ContactEmail        NVARCHAR(256)     NULL,
    ContactPhone        NVARCHAR(20)      NULL,
    LogoUrl             NVARCHAR(500)     NULL,
    EstablishedDate     DATETIME2         NULL,
    CreatedAt           DATETIME2         NOT NULL CONSTRAINT DF_Societies_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy           NVARCHAR(256)     NULL,
    ModifiedAt          DATETIME2         NULL,
    ModifiedBy          NVARCHAR(256)     NULL,
    IsDeleted           BIT               NOT NULL CONSTRAINT DF_Societies_IsDeleted DEFAULT (0),
    DeletedAt           DATETIME2         NULL,
    DeletedBy           NVARCHAR(256)     NULL
);
GO

CREATE TABLE dbo.Buildings
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Buildings PRIMARY KEY,
    SocietyId    INT               NOT NULL,
    Name         NVARCHAR(100)     NOT NULL,
    Description  NVARCHAR(250)     NULL,
    CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_Buildings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy    NVARCHAR(256)     NULL,
    ModifiedAt   DATETIME2         NULL,
    ModifiedBy   NVARCHAR(256)     NULL,
    IsDeleted    BIT               NOT NULL CONSTRAINT DF_Buildings_IsDeleted DEFAULT (0),
    DeletedAt    DATETIME2         NULL,
    DeletedBy    NVARCHAR(256)     NULL,
    CONSTRAINT FK_Buildings_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE INDEX IX_Buildings_SocietyId ON dbo.Buildings(SocietyId);
GO

CREATE TABLE dbo.Wings
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Wings PRIMARY KEY,
    BuildingId   INT               NOT NULL,
    Name         NVARCHAR(50)      NOT NULL,
    CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_Wings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy    NVARCHAR(256)     NULL,
    ModifiedAt   DATETIME2         NULL,
    ModifiedBy   NVARCHAR(256)     NULL,
    IsDeleted    BIT               NOT NULL CONSTRAINT DF_Wings_IsDeleted DEFAULT (0),
    DeletedAt    DATETIME2         NULL,
    DeletedBy    NVARCHAR(256)     NULL,
    CONSTRAINT FK_Wings_Buildings FOREIGN KEY (BuildingId) REFERENCES dbo.Buildings(Id)
);
GO
CREATE INDEX IX_Wings_BuildingId ON dbo.Wings(BuildingId);
GO

CREATE TABLE dbo.Floors
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Floors PRIMARY KEY,
    WingId       INT               NOT NULL,
    FloorNumber  INT               NOT NULL,
    Name         NVARCHAR(50)      NULL,
    CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_Floors_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy    NVARCHAR(256)     NULL,
    ModifiedAt   DATETIME2         NULL,
    ModifiedBy   NVARCHAR(256)     NULL,
    IsDeleted    BIT               NOT NULL CONSTRAINT DF_Floors_IsDeleted DEFAULT (0),
    DeletedAt    DATETIME2         NULL,
    DeletedBy    NVARCHAR(256)     NULL,
    CONSTRAINT FK_Floors_Wings FOREIGN KEY (WingId) REFERENCES dbo.Wings(Id)
);
GO
CREATE INDEX IX_Floors_WingId ON dbo.Floors(WingId);
GO

CREATE TABLE dbo.Flats
(
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Flats PRIMARY KEY,
    FloorId        INT               NOT NULL,
    FlatNumber     NVARCHAR(20)      NOT NULL,
    FlatType       TINYINT           NOT NULL, -- see Domain.Enums.FlatType
    AreaSqFt       DECIMAL(10,2)     NULL,
    Status         TINYINT           NOT NULL CONSTRAINT DF_Flats_Status DEFAULT (1), -- see Domain.Enums.FlatStatus
    OwnerMemberId  INT               NULL, -- forward FK to Members table, added by Module 2's migration
    CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_Flats_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy      NVARCHAR(256)     NULL,
    ModifiedAt     DATETIME2         NULL,
    ModifiedBy     NVARCHAR(256)     NULL,
    IsDeleted      BIT               NOT NULL CONSTRAINT DF_Flats_IsDeleted DEFAULT (0),
    DeletedAt      DATETIME2         NULL,
    DeletedBy      NVARCHAR(256)     NULL,
    CONSTRAINT FK_Flats_Floors FOREIGN KEY (FloorId) REFERENCES dbo.Floors(Id)
);
GO
CREATE UNIQUE INDEX UX_Flats_Floor_Number ON dbo.Flats(FloorId, FlatNumber) WHERE IsDeleted = 0;
CREATE INDEX IX_Flats_Status ON dbo.Flats(Status);
GO

CREATE TABLE dbo.ParkingSlots
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ParkingSlots PRIMARY KEY,
    SocietyId        INT               NOT NULL,
    SlotNumber       NVARCHAR(20)      NOT NULL,
    Type             TINYINT           NOT NULL, -- see Domain.Enums.ParkingType
    Status           TINYINT           NOT NULL CONSTRAINT DF_ParkingSlots_Status DEFAULT (1), -- see Domain.Enums.ParkingStatus
    AllocatedFlatId  INT               NULL,
    CreatedAt        DATETIME2         NOT NULL CONSTRAINT DF_ParkingSlots_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy        NVARCHAR(256)     NULL,
    ModifiedAt       DATETIME2         NULL,
    ModifiedBy       NVARCHAR(256)     NULL,
    IsDeleted        BIT               NOT NULL CONSTRAINT DF_ParkingSlots_IsDeleted DEFAULT (0),
    DeletedAt        DATETIME2         NULL,
    DeletedBy        NVARCHAR(256)     NULL,
    CONSTRAINT FK_ParkingSlots_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id),
    CONSTRAINT FK_ParkingSlots_Flats FOREIGN KEY (AllocatedFlatId) REFERENCES dbo.Flats(Id) ON DELETE SET NULL
);
GO
CREATE UNIQUE INDEX UX_ParkingSlots_Society_Number ON dbo.ParkingSlots(SocietyId, SlotNumber) WHERE IsDeleted = 0;
GO

PRINT 'Foundation schema created successfully.';
