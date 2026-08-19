/* =============================================================================
   Resident Management — Module 2 Schema
   Target: Microsoft SQL Server 2019+
   Covers: Members, FlatResidencies (the owner/tenant/family-member join that
   answers "is this flat rented, by whom, how many people live here, and
   who owned it before"), Vehicles, EmergencyContacts, FlatResaleListings.
   Run AFTER 01_CreateSchema.sql, 02_CreateFestivalSchema.sql,
   03_CreateMaintenanceSchema.sql (depends on dbo.Societies, dbo.Flats,
   dbo.Users, dbo.ParkingSlots).
   Every business table carries the mandatory audit columns
   (CreatedAt/CreatedBy/ModifiedAt/ModifiedBy/IsDeleted) per spec.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   Members — a person registered against the society. Independent of any
   single flat; the relationship lives on FlatResidencies below.
   Gender: 1=Male 2=Female 3=Other (Domain.Enums.Gender, already defined)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Members
(
    Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Members PRIMARY KEY,
    SocietyId     INT               NOT NULL,
    FirstName     NVARCHAR(100)     NOT NULL,
    LastName      NVARCHAR(100)     NOT NULL,
    Phone         NVARCHAR(20)      NOT NULL,
    Email         NVARCHAR(256)     NULL,
    Gender        TINYINT           NULL,
    DateOfBirth   DATETIME2         NULL,
    PhotoUrl      NVARCHAR(500)     NULL,
    UserId        INT               NULL,
    CreatedAt     DATETIME2         NOT NULL CONSTRAINT DF_Members_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy     NVARCHAR(256)     NULL,
    ModifiedAt    DATETIME2         NULL,
    ModifiedBy    NVARCHAR(256)     NULL,
    IsDeleted     BIT               NOT NULL CONSTRAINT DF_Members_IsDeleted DEFAULT (0),
    DeletedAt     DATETIME2         NULL,
    DeletedBy     NVARCHAR(256)     NULL,
    CONSTRAINT FK_Members_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE INDEX IX_Members_Society_Phone ON dbo.Members(SocietyId, Phone);
GO

/* Users.MemberId was created as a placeholder nullable column with no FK by
   01_CreateSchema.sql ("forward FK to Members table, added by Module 2's
   migration") — add the real FK constraint + unique index now that Members
   exists (one User links to at most one Member). */
ALTER TABLE dbo.Users ADD CONSTRAINT FK_Users_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(Id) ON DELETE SET NULL;
GO
CREATE UNIQUE INDEX UX_Users_MemberId ON dbo.Users(MemberId) WHERE MemberId IS NOT NULL;
GO

/* Flats.OwnerMemberId was the same kind of placeholder — a single nullable
   FK can't represent joint ownership, a tenant living in an owner's flat,
   or ownership history. Replaced by FlatResidencies below; the column is
   dropped since it was never populated by any real code path. */
ALTER TABLE dbo.Flats DROP COLUMN OwnerMemberId;
GO

/* ---------------------------------------------------------------------------
   FlatResidencies — the source of truth for "is this flat rented, by whom",
   "how many people live here" (COUNT of current rows), and "history of
   flat owners" (all MemberType=Owner rows), all answered by querying this
   one table rather than storing redundant fields.
   MemberType: 1=Owner 2=Tenant 3=FamilyMember (Domain.Enums.MemberType,
   already defined)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FlatResidencies
(
    Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FlatResidencies PRIMARY KEY,
    FlatId            INT               NOT NULL,
    MemberId          INT               NOT NULL,
    MemberType        TINYINT           NOT NULL,
    MoveInDate        DATETIME2         NOT NULL,
    MoveOutDate       DATETIME2         NULL,
    IsPrimaryContact  BIT               NOT NULL CONSTRAINT DF_FlatResidencies_IsPrimary DEFAULT (0),
    Notes             NVARCHAR(500)     NULL,
    CreatedAt         DATETIME2         NOT NULL CONSTRAINT DF_FlatResidencies_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy         NVARCHAR(256)     NULL,
    ModifiedAt        DATETIME2         NULL,
    ModifiedBy        NVARCHAR(256)     NULL,
    IsDeleted         BIT               NOT NULL CONSTRAINT DF_FlatResidencies_IsDeleted DEFAULT (0),
    DeletedAt         DATETIME2         NULL,
    DeletedBy         NVARCHAR(256)     NULL,
    CONSTRAINT FK_FlatResidencies_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id),
    CONSTRAINT FK_FlatResidencies_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(Id)
);
GO
CREATE INDEX IX_FlatResidencies_Flat_MoveOut ON dbo.FlatResidencies(FlatId, MoveOutDate);
CREATE INDEX IX_FlatResidencies_MemberId ON dbo.FlatResidencies(MemberId);
GO

/* ---------------------------------------------------------------------------
   Vehicles.
   VehicleType: 1=TwoWheeler 2=FourWheeler (Domain.Enums.VehicleType)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Vehicles
(
    Id                    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vehicles PRIMARY KEY,
    MemberId              INT               NOT NULL,
    VehicleType           TINYINT           NOT NULL,
    RegistrationNumber    NVARCHAR(20)      NOT NULL,
    Make                  NVARCHAR(100)     NULL,
    Model                 NVARCHAR(100)     NULL,
    Color                 NVARCHAR(50)      NULL,
    ParkingSlotId         INT               NULL,
    CreatedAt             DATETIME2         NOT NULL CONSTRAINT DF_Vehicles_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy             NVARCHAR(256)     NULL,
    ModifiedAt            DATETIME2         NULL,
    ModifiedBy            NVARCHAR(256)     NULL,
    IsDeleted             BIT               NOT NULL CONSTRAINT DF_Vehicles_IsDeleted DEFAULT (0),
    DeletedAt             DATETIME2         NULL,
    DeletedBy             NVARCHAR(256)     NULL,
    CONSTRAINT FK_Vehicles_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(Id),
    CONSTRAINT FK_Vehicles_ParkingSlots FOREIGN KEY (ParkingSlotId) REFERENCES dbo.ParkingSlots(Id) ON DELETE SET NULL
);
GO
CREATE INDEX IX_Vehicles_MemberId ON dbo.Vehicles(MemberId);
CREATE UNIQUE INDEX UX_Vehicles_RegistrationNumber ON dbo.Vehicles(RegistrationNumber) WHERE IsDeleted = 0;
GO

/* ---------------------------------------------------------------------------
   EmergencyContacts.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.EmergencyContacts
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmergencyContacts PRIMARY KEY,
    FlatId          INT               NOT NULL,
    ContactName     NVARCHAR(150)     NOT NULL,
    Relationship    NVARCHAR(50)      NOT NULL,
    Phone           NVARCHAR(20)      NOT NULL,
    AlternatePhone  NVARCHAR(20)      NULL,
    CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_EmergencyContacts_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy       NVARCHAR(256)     NULL,
    ModifiedAt      DATETIME2         NULL,
    ModifiedBy      NVARCHAR(256)     NULL,
    IsDeleted       BIT               NOT NULL CONSTRAINT DF_EmergencyContacts_IsDeleted DEFAULT (0),
    DeletedAt       DATETIME2         NULL,
    DeletedBy       NVARCHAR(256)     NULL,
    CONSTRAINT FK_EmergencyContacts_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id)
);
GO
CREATE INDEX IX_EmergencyContacts_FlatId ON dbo.EmergencyContacts(FlatId);
GO

/* ---------------------------------------------------------------------------
   FlatResaleListings.
   Status: 1=Available 2=UnderNegotiation 3=Sold 4=Withdrawn (Domain.Enums.ListingStatus)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FlatResaleListings
(
    Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FlatResaleListings PRIMARY KEY,
    FlatId             INT               NOT NULL,
    ListedByMemberId   INT               NOT NULL,
    AskingPrice        DECIMAL(14,2)     NOT NULL,
    AvailableFrom      DATETIME2         NOT NULL,
    VisitTimingNotes   NVARCHAR(250)     NULL,
    Status             TINYINT           NOT NULL CONSTRAINT DF_FlatResaleListings_Status DEFAULT (1),
    NocRequested       BIT               NOT NULL CONSTRAINT DF_FlatResaleListings_NocRequested DEFAULT (0),
    NocIssuedDate      DATETIME2         NULL,
    NocDocumentUrl     NVARCHAR(500)     NULL,
    Notes              NVARCHAR(500)     NULL,
    CreatedAt          DATETIME2         NOT NULL CONSTRAINT DF_FlatResaleListings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy          NVARCHAR(256)     NULL,
    ModifiedAt         DATETIME2         NULL,
    ModifiedBy         NVARCHAR(256)     NULL,
    IsDeleted          BIT               NOT NULL CONSTRAINT DF_FlatResaleListings_IsDeleted DEFAULT (0),
    DeletedAt          DATETIME2         NULL,
    DeletedBy          NVARCHAR(256)     NULL,
    CONSTRAINT FK_FlatResaleListings_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id),
    CONSTRAINT FK_FlatResaleListings_Members FOREIGN KEY (ListedByMemberId) REFERENCES dbo.Members(Id)
);
GO
CREATE INDEX IX_FlatResaleListings_Flat_Status ON dbo.FlatResaleListings(FlatId, Status);
GO

PRINT 'Resident Management (Module 2) schema created successfully.';
