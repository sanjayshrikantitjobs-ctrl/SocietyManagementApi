/* =============================================================================
   Visitor & Gate Management — Module 4 Schema (Phase 1: core approval workflow)
   Target: Microsoft SQL Server 2019+
   Covers: Gates, VisitorPurposes (configurable, RequiresApproval per
   purpose), Visitors (the reusable person record), VisitorVisits (one
   gate-entry attempt through PendingApproval -> Approved -> CheckedIn ->
   CheckedOut, or -> Rejected / Expired / Cancelled), VisitorSettings (one
   row per society — currently just ApprovalRequestExpiryMinutes).
   Run AFTER 01_CreateSchema.sql, 04_CreateResidentSchema.sql (depends on
   dbo.Societies, dbo.Flats, dbo.Members, dbo.Users).
   Every business table carries the mandatory audit columns
   (CreatedAt/CreatedBy/ModifiedAt/ModifiedBy/IsDeleted) per spec.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   Gates.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Gates
(
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Gates PRIMARY KEY,
    SocietyId   INT               NOT NULL,
    Name        NVARCHAR(100)     NOT NULL,
    Code        NVARCHAR(30)      NOT NULL,
    Location    NVARCHAR(200)     NULL,
    IsActive    BIT               NOT NULL CONSTRAINT DF_Gates_IsActive DEFAULT (1),
    CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_Gates_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(256)     NULL,
    ModifiedAt  DATETIME2         NULL,
    ModifiedBy  NVARCHAR(256)     NULL,
    IsDeleted   BIT               NOT NULL CONSTRAINT DF_Gates_IsDeleted DEFAULT (0),
    DeletedAt   DATETIME2         NULL,
    DeletedBy   NVARCHAR(256)     NULL,
    CONSTRAINT FK_Gates_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE UNIQUE INDEX UX_Gates_Society_Code ON dbo.Gates(SocietyId, Code) WHERE IsDeleted = 0;
GO

/* ---------------------------------------------------------------------------
   VisitorPurposes — configurable, not hardcoded. RequiresApproval = 0 skips
   resident approval entirely (e.g. a known daily vendor).
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.VisitorPurposes
(
    Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VisitorPurposes PRIMARY KEY,
    SocietyId         INT               NOT NULL,
    Name              NVARCHAR(100)     NOT NULL,
    RequiresApproval  BIT               NOT NULL CONSTRAINT DF_VisitorPurposes_RequiresApproval DEFAULT (1),
    IsActive          BIT               NOT NULL CONSTRAINT DF_VisitorPurposes_IsActive DEFAULT (1),
    DisplayOrder      INT               NOT NULL CONSTRAINT DF_VisitorPurposes_DisplayOrder DEFAULT (0),
    CreatedAt         DATETIME2         NOT NULL CONSTRAINT DF_VisitorPurposes_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy         NVARCHAR(256)     NULL,
    ModifiedAt        DATETIME2         NULL,
    ModifiedBy        NVARCHAR(256)     NULL,
    IsDeleted         BIT               NOT NULL CONSTRAINT DF_VisitorPurposes_IsDeleted DEFAULT (0),
    DeletedAt         DATETIME2         NULL,
    DeletedBy         NVARCHAR(256)     NULL,
    CONSTRAINT FK_VisitorPurposes_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE UNIQUE INDEX UX_VisitorPurposes_Society_Name ON dbo.VisitorPurposes(SocietyId, Name) WHERE IsDeleted = 0;
GO

/* ---------------------------------------------------------------------------
   Visitors — a person, independent of any single visit; reused across
   repeat visits and, in later phases, frequent-visitor/domestic-help
   records.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Visitors
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Visitors PRIMARY KEY,
    SocietyId       INT               NOT NULL,
    Name            NVARCHAR(150)     NOT NULL,
    MobileNumber    NVARCHAR(20)      NOT NULL,
    PhotoUrl        NVARCHAR(500)     NULL,
    VehicleNumber   NVARCHAR(20)      NULL,
    VehicleType     NVARCHAR(50)      NULL,
    IdType          NVARCHAR(50)      NULL,
    IdReference     NVARCHAR(100)     NULL,
    Notes           NVARCHAR(500)     NULL,
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Visitors_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy       NVARCHAR(256)     NULL,
    ModifiedAt      DATETIME2         NULL,
    ModifiedBy      NVARCHAR(256)     NULL,
    IsDeleted       BIT               NOT NULL CONSTRAINT DF_Visitors_IsDeleted DEFAULT (0),
    DeletedAt       DATETIME2         NULL,
    DeletedBy       NVARCHAR(256)     NULL,
    CONSTRAINT FK_Visitors_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE INDEX IX_Visitors_Society_Mobile ON dbo.Visitors(SocietyId, MobileNumber);
CREATE INDEX IX_Visitors_VehicleNumber ON dbo.Visitors(VehicleNumber);
GO

/* ---------------------------------------------------------------------------
   VisitorVisits — one gate-entry attempt.
   Status: 1=PendingApproval 2=Approved 3=Rejected 4=CheckedIn 5=CheckedOut
   6=Expired 7=Cancelled (Domain.Enums.VisitorVisitStatus). Valid
   transitions: PendingApproval -> Approved -> CheckedIn -> CheckedOut;
   PendingApproval -> Rejected / Expired / Cancelled. No other transition
   is allowed.
   CreatedByUserId is the watchman who created the request — the target for
   real-time approve/reject/expire notifications back to the gate; distinct
   from the inherited CreatedBy display string.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.VisitorVisits
(
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VisitorVisits PRIMARY KEY,
    SocietyId           INT               NOT NULL,
    VisitorId           INT               NOT NULL,
    FlatId              INT               NOT NULL,
    PurposeId           INT               NOT NULL,
    GateId              INT               NOT NULL,
    NumberOfVisitors    INT               NOT NULL CONSTRAINT DF_VisitorVisits_NumberOfVisitors DEFAULT (1),
    Status              TINYINT           NOT NULL CONSTRAINT DF_VisitorVisits_Status DEFAULT (1),
    CreatedByUserId     INT               NOT NULL,
    RequestedAt         DATETIME2         NOT NULL,
    ApprovedAt          DATETIME2         NULL,
    ApprovedByUserId    INT               NULL,
    RejectedAt          DATETIME2         NULL,
    RejectedByUserId    INT               NULL,
    RejectionReason     NVARCHAR(500)     NULL,
    CheckInTime         DATETIME2         NULL,
    CheckedInByUserId   INT               NULL,
    CheckOutTime        DATETIME2         NULL,
    CheckedOutByUserId  INT               NULL,
    CreatedAt           DATETIME2         NOT NULL CONSTRAINT DF_VisitorVisits_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy           NVARCHAR(256)     NULL,
    ModifiedAt          DATETIME2         NULL,
    ModifiedBy           NVARCHAR(256)    NULL,
    IsDeleted           BIT               NOT NULL CONSTRAINT DF_VisitorVisits_IsDeleted DEFAULT (0),
    DeletedAt           DATETIME2         NULL,
    DeletedBy           NVARCHAR(256)     NULL,
    CONSTRAINT FK_VisitorVisits_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id),
    CONSTRAINT FK_VisitorVisits_Visitors FOREIGN KEY (VisitorId) REFERENCES dbo.Visitors(Id),
    CONSTRAINT FK_VisitorVisits_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id),
    CONSTRAINT FK_VisitorVisits_Purposes FOREIGN KEY (PurposeId) REFERENCES dbo.VisitorPurposes(Id),
    CONSTRAINT FK_VisitorVisits_Gates FOREIGN KEY (GateId) REFERENCES dbo.Gates(Id),
    CONSTRAINT FK_VisitorVisits_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_VisitorVisits_ApprovedByUser FOREIGN KEY (ApprovedByUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_VisitorVisits_RejectedByUser FOREIGN KEY (RejectedByUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_VisitorVisits_CheckedInByUser FOREIGN KEY (CheckedInByUserId) REFERENCES dbo.Users(Id),
    CONSTRAINT FK_VisitorVisits_CheckedOutByUser FOREIGN KEY (CheckedOutByUserId) REFERENCES dbo.Users(Id)
);
GO
CREATE INDEX IX_VisitorVisits_Society_Status ON dbo.VisitorVisits(SocietyId, Status);
CREATE INDEX IX_VisitorVisits_Flat_Status ON dbo.VisitorVisits(FlatId, Status);
CREATE INDEX IX_VisitorVisits_GateId ON dbo.VisitorVisits(GateId);
CREATE INDEX IX_VisitorVisits_RequestedAt ON dbo.VisitorVisits(RequestedAt);
GO

/* ---------------------------------------------------------------------------
   VisitorSettings — one row per society.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.VisitorSettings
(
    Id                              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VisitorSettings PRIMARY KEY,
    SocietyId                       INT               NOT NULL,
    ApprovalRequestExpiryMinutes    INT               NOT NULL CONSTRAINT DF_VisitorSettings_ExpiryMinutes DEFAULT (30),
    CreatedAt                       DATETIME2         NOT NULL CONSTRAINT DF_VisitorSettings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy                       NVARCHAR(256)     NULL,
    ModifiedAt                      DATETIME2         NULL,
    ModifiedBy                      NVARCHAR(256)     NULL,
    IsDeleted                       BIT               NOT NULL CONSTRAINT DF_VisitorSettings_IsDeleted DEFAULT (0),
    DeletedAt                       DATETIME2         NULL,
    DeletedBy                       NVARCHAR(256)     NULL,
    CONSTRAINT FK_VisitorSettings_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE UNIQUE INDEX UX_VisitorSettings_SocietyId ON dbo.VisitorSettings(SocietyId) WHERE IsDeleted = 0;
GO

PRINT 'Visitor & Gate Management (Module 4, Phase 1) schema created successfully.';
