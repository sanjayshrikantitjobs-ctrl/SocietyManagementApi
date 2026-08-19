/* =============================================================================
   Maintenance Management — Module 1 Schema
   Target: Microsoft SQL Server 2019+
   Covers: Maintenance Categories, Settings, Special Charges, Fines, Bills
   (+ Items + Payments), and the Flat owner-contact columns billing/WhatsApp
   delivery reads from.
   Run AFTER 01_CreateSchema.sql and 02_CreateFestivalSchema.sql (depends on
   dbo.Societies, dbo.Flats, dbo.Users).
   Every business table carries the mandatory audit columns
   (CreatedAt/CreatedBy/ModifiedAt/ModifiedBy/IsDeleted) per spec.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   Flat owner contact columns — Member Management doesn't exist yet, so
   billing/WhatsApp delivery reads these directly until OwnerMemberId is a
   real FK.
   --------------------------------------------------------------------------- */
ALTER TABLE dbo.Flats ADD
    OwnerName  NVARCHAR(150) NULL,
    OwnerPhone NVARCHAR(20)  NULL,
    OwnerEmail NVARCHAR(256) NULL;
GO

/* ---------------------------------------------------------------------------
   MaintenanceCategories — recurring charge lines every flat gets billed for.
   ChargeType: 1=Fixed 2=PerSqFt 3=OneTime (Domain.Enums.ChargeType)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.MaintenanceCategories
(
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceCategories PRIMARY KEY,
    SocietyId      INT               NOT NULL,
    ChargeName     NVARCHAR(150)     NOT NULL,
    ChargeType     TINYINT           NOT NULL,
    MonthlyAmount  DECIMAL(12,2)     NOT NULL CONSTRAINT DF_MaintenanceCategories_Amount DEFAULT (0),
    EffectiveFrom  DATETIME2         NOT NULL,
    IsActive       BIT               NOT NULL CONSTRAINT DF_MaintenanceCategories_IsActive DEFAULT (1),
    DisplayOrder   INT               NOT NULL CONSTRAINT DF_MaintenanceCategories_DisplayOrder DEFAULT (0),
    CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_MaintenanceCategories_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy      NVARCHAR(256)     NULL,
    ModifiedAt     DATETIME2         NULL,
    ModifiedBy     NVARCHAR(256)     NULL,
    IsDeleted      BIT               NOT NULL CONSTRAINT DF_MaintenanceCategories_IsDeleted DEFAULT (0),
    DeletedAt      DATETIME2         NULL,
    DeletedBy      NVARCHAR(256)     NULL,
    CONSTRAINT FK_MaintenanceCategories_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE INDEX IX_MaintenanceCategories_Society_Order ON dbo.MaintenanceCategories(SocietyId, DisplayOrder);
GO

/* ---------------------------------------------------------------------------
   MaintenanceSettings — one row per society.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.MaintenanceSettings
(
    Id                      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceSettings PRIMARY KEY,
    SocietyId               INT               NOT NULL,
    BillGenerationDay       INT               NOT NULL CONSTRAINT DF_MaintenanceSettings_BillGenDay DEFAULT (1),
    DueDay                  INT               NOT NULL CONSTRAINT DF_MaintenanceSettings_DueDay DEFAULT (10),
    GracePeriodDays         INT               NOT NULL CONSTRAINT DF_MaintenanceSettings_Grace DEFAULT (0),
    LateFeeAmount           DECIMAL(12,2)     NOT NULL CONSTRAINT DF_MaintenanceSettings_LateFee DEFAULT (0),
    InvoiceNumberPrefix     NVARCHAR(20)      NOT NULL CONSTRAINT DF_MaintenanceSettings_Prefix DEFAULT ('INV'),
    NextInvoiceNumber       INT               NOT NULL CONSTRAINT DF_MaintenanceSettings_NextInv DEFAULT (1),
    WhatsAppMessageTemplate NVARCHAR(1000)    NOT NULL,
    PdfFooterMessage        NVARCHAR(500)     NOT NULL,
    CreatedAt               DATETIME2         NOT NULL CONSTRAINT DF_MaintenanceSettings_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy               NVARCHAR(256)     NULL,
    ModifiedAt              DATETIME2         NULL,
    ModifiedBy              NVARCHAR(256)     NULL,
    IsDeleted               BIT               NOT NULL CONSTRAINT DF_MaintenanceSettings_IsDeleted DEFAULT (0),
    DeletedAt               DATETIME2         NULL,
    DeletedBy               NVARCHAR(256)     NULL,
    CONSTRAINT FK_MaintenanceSettings_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE UNIQUE INDEX UX_MaintenanceSettings_SocietyId ON dbo.MaintenanceSettings(SocietyId) WHERE IsDeleted = 0;
GO

/* ---------------------------------------------------------------------------
   SpecialCharges — assigned to one specific flat only (Parking, Club House,
   Generator, Repair, custom).
   Frequency: 1=Monthly 2=OneTime (Domain.Enums.ChargeFrequency)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.SpecialCharges
(
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SpecialCharges PRIMARY KEY,
    FlatId       INT               NOT NULL,
    ChargeName   NVARCHAR(150)     NOT NULL,
    Amount       DECIMAL(12,2)     NOT NULL,
    Frequency    TINYINT           NOT NULL,
    StartDate    DATETIME2         NOT NULL,
    EndDate      DATETIME2         NULL,
    Notes        NVARCHAR(500)     NULL,
    IsActive     BIT               NOT NULL CONSTRAINT DF_SpecialCharges_IsActive DEFAULT (1),
    CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_SpecialCharges_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy    NVARCHAR(256)     NULL,
    ModifiedAt   DATETIME2         NULL,
    ModifiedBy   NVARCHAR(256)     NULL,
    IsDeleted    BIT               NOT NULL CONSTRAINT DF_SpecialCharges_IsDeleted DEFAULT (0),
    DeletedAt    DATETIME2         NULL,
    DeletedBy    NVARCHAR(256)     NULL,
    CONSTRAINT FK_SpecialCharges_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id)
);
GO
CREATE INDEX IX_SpecialCharges_FlatId ON dbo.SpecialCharges(FlatId);
GO

/* ---------------------------------------------------------------------------
   FineRecords.
   Status: 1=Pending 2=Billed 3=Waived (Domain.Enums.FineStatus)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FineRecords
(
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FineRecords PRIMARY KEY,
    FlatId      INT               NOT NULL,
    Reason      NVARCHAR(250)     NOT NULL,
    Amount      DECIMAL(12,2)     NOT NULL,
    FineDate    DATETIME2         NOT NULL,
    Status      TINYINT           NOT NULL CONSTRAINT DF_FineRecords_Status DEFAULT (1),
    CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_FineRecords_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(256)     NULL,
    ModifiedAt  DATETIME2         NULL,
    ModifiedBy  NVARCHAR(256)     NULL,
    IsDeleted   BIT               NOT NULL CONSTRAINT DF_FineRecords_IsDeleted DEFAULT (0),
    DeletedAt   DATETIME2         NULL,
    DeletedBy   NVARCHAR(256)     NULL,
    CONSTRAINT FK_FineRecords_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id)
);
GO
CREATE INDEX IX_FineRecords_Flat_Status ON dbo.FineRecords(FlatId, Status);
GO

/* ---------------------------------------------------------------------------
   MaintenanceBills — one flat's bill for one month.
   Status: 1=Pending 2=PartiallyPaid 3=Paid 4=Overdue (Domain.Enums.BillStatus
   — Overdue is only ever set by the read path/derived from DueDate, never
   persisted by the write path; kept as a stored enum value for completeness).
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.MaintenanceBills
(
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceBills PRIMARY KEY,
    FlatId              INT               NOT NULL,
    BillMonth           DATETIME2         NOT NULL,
    InvoiceNumber       NVARCHAR(30)      NOT NULL,
    PreviousBalance     DECIMAL(12,2)     NOT NULL CONSTRAINT DF_MaintenanceBills_PrevBal DEFAULT (0),
    FineAmount          DECIMAL(12,2)     NOT NULL CONSTRAINT DF_MaintenanceBills_Fine DEFAULT (0),
    TotalAmount         DECIMAL(12,2)     NOT NULL,
    AmountPaid          DECIMAL(12,2)     NOT NULL CONSTRAINT DF_MaintenanceBills_AmountPaid DEFAULT (0),
    DueDate             DATETIME2         NOT NULL,
    Status              TINYINT           NOT NULL CONSTRAINT DF_MaintenanceBills_Status DEFAULT (1),
    PdfUrl              NVARCHAR(500)     NULL,
    GeneratedAt         DATETIME2         NOT NULL,
    OwnerNameSnapshot   NVARCHAR(150)     NULL,
    OwnerPhoneSnapshot  NVARCHAR(20)      NULL,
    CreatedAt           DATETIME2         NOT NULL CONSTRAINT DF_MaintenanceBills_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy           NVARCHAR(256)     NULL,
    ModifiedAt          DATETIME2         NULL,
    ModifiedBy          NVARCHAR(256)     NULL,
    IsDeleted           BIT               NOT NULL CONSTRAINT DF_MaintenanceBills_IsDeleted DEFAULT (0),
    DeletedAt           DATETIME2         NULL,
    DeletedBy           NVARCHAR(256)     NULL,
    CONSTRAINT FK_MaintenanceBills_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id)
);
GO
CREATE UNIQUE INDEX UX_MaintenanceBills_InvoiceNumber ON dbo.MaintenanceBills(InvoiceNumber);
CREATE UNIQUE INDEX UX_MaintenanceBills_Flat_Month ON dbo.MaintenanceBills(FlatId, BillMonth) WHERE IsDeleted = 0;
CREATE INDEX IX_MaintenanceBills_Status ON dbo.MaintenanceBills(Status);
GO

/* ---------------------------------------------------------------------------
   MaintenanceBillItems — the itemized breakdown on a bill.
   ItemType: 1=Category 2=SpecialCharge 3=Fine (Domain.Enums.BillItemType)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.MaintenanceBillItems
(
    Id                      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceBillItems PRIMARY KEY,
    MaintenanceBillId       INT               NOT NULL,
    Description             NVARCHAR(250)     NOT NULL,
    Amount                  DECIMAL(12,2)     NOT NULL,
    ItemType                TINYINT           NOT NULL,
    MaintenanceCategoryId   INT               NULL,
    SpecialChargeId         INT               NULL,
    FineRecordId            INT               NULL,
    CreatedAt               DATETIME2         NOT NULL CONSTRAINT DF_MaintenanceBillItems_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy               NVARCHAR(256)     NULL,
    ModifiedAt              DATETIME2         NULL,
    ModifiedBy              NVARCHAR(256)     NULL,
    IsDeleted               BIT               NOT NULL CONSTRAINT DF_MaintenanceBillItems_IsDeleted DEFAULT (0),
    DeletedAt               DATETIME2         NULL,
    DeletedBy               NVARCHAR(256)     NULL,
    CONSTRAINT FK_MaintenanceBillItems_Bills FOREIGN KEY (MaintenanceBillId) REFERENCES dbo.MaintenanceBills(Id),
    CONSTRAINT FK_MaintenanceBillItems_Categories FOREIGN KEY (MaintenanceCategoryId) REFERENCES dbo.MaintenanceCategories(Id) ON DELETE SET NULL,
    CONSTRAINT FK_MaintenanceBillItems_SpecialCharges FOREIGN KEY (SpecialChargeId) REFERENCES dbo.SpecialCharges(Id) ON DELETE SET NULL,
    CONSTRAINT FK_MaintenanceBillItems_Fines FOREIGN KEY (FineRecordId) REFERENCES dbo.FineRecords(Id) ON DELETE SET NULL
);
GO
CREATE INDEX IX_MaintenanceBillItems_BillId ON dbo.MaintenanceBillItems(MaintenanceBillId);
GO

/* ---------------------------------------------------------------------------
   MaintenancePayments.
   PaymentMode: 1=Cash 2=UPI 3=BankTransfer 4=Cheque (Domain.Enums.PaymentMode)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.MaintenancePayments
(
    Id                     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenancePayments PRIMARY KEY,
    MaintenanceBillId      INT               NOT NULL,
    Amount                 DECIMAL(12,2)     NOT NULL,
    PaymentDate            DATETIME2         NOT NULL,
    PaymentMode            TINYINT           NOT NULL,
    TransactionReference   NVARCHAR(100)     NULL,
    ReceivedByUserId       INT               NULL,
    Notes                  NVARCHAR(500)     NULL,
    CreatedAt              DATETIME2         NOT NULL CONSTRAINT DF_MaintenancePayments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy              NVARCHAR(256)     NULL,
    ModifiedAt             DATETIME2         NULL,
    ModifiedBy             NVARCHAR(256)     NULL,
    IsDeleted              BIT               NOT NULL CONSTRAINT DF_MaintenancePayments_IsDeleted DEFAULT (0),
    DeletedAt              DATETIME2         NULL,
    DeletedBy              NVARCHAR(256)     NULL,
    CONSTRAINT FK_MaintenancePayments_Bills FOREIGN KEY (MaintenanceBillId) REFERENCES dbo.MaintenanceBills(Id),
    CONSTRAINT FK_MaintenancePayments_Users FOREIGN KEY (ReceivedByUserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
);
GO
CREATE INDEX IX_MaintenancePayments_BillId ON dbo.MaintenancePayments(MaintenanceBillId);
GO

PRINT 'Maintenance Management (Module 1) schema created successfully.';
