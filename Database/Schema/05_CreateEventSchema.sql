/* =============================================================================
   Events — Module 3 Schema
   Target: Microsoft SQL Server 2019+
   Covers: Events (a dated, capacity-limited happening — optionally funded
   by / associated with a Festival, but stands on its own so it's reusable
   beyond festival-funded gatherings) and EventRsvps (one flat's headcount
   registration per event, carrying the QR check-in token).
   Run AFTER 01_CreateSchema.sql, 02_CreateFestivalSchema.sql,
   03_CreateMaintenanceSchema.sql, 04_CreateResidentSchema.sql (depends on
   dbo.Societies, dbo.Festivals, dbo.Flats, dbo.Members, dbo.Users).
   Every business table carries the mandatory audit columns
   (CreatedAt/CreatedBy/ModifiedAt/ModifiedBy/IsDeleted) per spec.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   Events.
   Status: 1=Draft 2=Open 3=Closed 4=Completed 5=Cancelled (Domain.Enums.EventStatus)
   CapacityLimit NULL = unlimited attendance.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Events
(
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Events PRIMARY KEY,
    SocietyId      INT               NOT NULL,
    FestivalId     INT               NULL,
    Name           NVARCHAR(150)     NOT NULL,
    Description    NVARCHAR(2000)    NULL,
    EventDateTime  DATETIME2         NOT NULL,
    Venue          NVARCHAR(200)     NULL,
    CapacityLimit  INT               NULL,
    RsvpDeadline   DATETIME2         NULL,
    Status         TINYINT           NOT NULL CONSTRAINT DF_Events_Status DEFAULT (1),
    CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy      NVARCHAR(256)     NULL,
    ModifiedAt     DATETIME2         NULL,
    ModifiedBy     NVARCHAR(256)     NULL,
    IsDeleted      BIT               NOT NULL CONSTRAINT DF_Events_IsDeleted DEFAULT (0),
    DeletedAt      DATETIME2         NULL,
    DeletedBy      NVARCHAR(256)     NULL,
    CONSTRAINT FK_Events_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id),
    CONSTRAINT FK_Events_Festivals FOREIGN KEY (FestivalId) REFERENCES dbo.Festivals(Id) ON DELETE SET NULL
);
GO
CREATE INDEX IX_Events_Society_Status ON dbo.Events(SocietyId, Status);
GO

/* ---------------------------------------------------------------------------
   EventRsvps — one row per (EventId, FlatId); resubmitting updates this same
   row rather than creating a duplicate. QrToken is the opaque value the
   flat's QR code encodes; the check-in endpoint looks the RSVP up by it.
   Status: 1=Registered 2=CheckedIn 3=Cancelled (Domain.Enums.EventRsvpStatus)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.EventRsvps
(
    Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventRsvps PRIMARY KEY,
    EventId            INT               NOT NULL,
    FlatId             INT               NOT NULL,
    MemberId           INT               NOT NULL,
    HeadCount          INT               NOT NULL,
    QrToken            NVARCHAR(64)      NOT NULL,
    Status             TINYINT           NOT NULL CONSTRAINT DF_EventRsvps_Status DEFAULT (1),
    CheckedInCount     INT               NULL,
    CheckedInAt        DATETIME2         NULL,
    CheckedInByUserId  INT               NULL,
    CreatedAt          DATETIME2         NOT NULL CONSTRAINT DF_EventRsvps_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy          NVARCHAR(256)     NULL,
    ModifiedAt         DATETIME2         NULL,
    ModifiedBy         NVARCHAR(256)     NULL,
    IsDeleted          BIT               NOT NULL CONSTRAINT DF_EventRsvps_IsDeleted DEFAULT (0),
    DeletedAt          DATETIME2         NULL,
    DeletedBy          NVARCHAR(256)     NULL,
    CONSTRAINT FK_EventRsvps_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(Id),
    CONSTRAINT FK_EventRsvps_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id),
    CONSTRAINT FK_EventRsvps_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(Id),
    CONSTRAINT FK_EventRsvps_CheckedInByUser FOREIGN KEY (CheckedInByUserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
);
GO
CREATE UNIQUE INDEX UX_EventRsvps_QrToken ON dbo.EventRsvps(QrToken);
CREATE UNIQUE INDEX UX_EventRsvps_Event_Flat ON dbo.EventRsvps(EventId, FlatId) WHERE IsDeleted = 0;
GO

PRINT 'Events (Module 3) schema created successfully.';
