/* =============================================================================
   Festival & Event Management — Phase 1 (Foundation) Schema
   Target: Microsoft SQL Server 2019+
   Covers: Festivals, Budget (categories + revision history), Member
   Contributions, Sponsors, Vendors, Expenses (with approval workflow).
   Run AFTER 01_CreateSchema.sql (depends on dbo.Societies, dbo.Flats, dbo.Users).
   Every business table carries the mandatory audit columns
   (CreatedAt/CreatedBy/ModifiedAt/ModifiedBy/IsDeleted) per spec.
   ============================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   Festivals — every festival/event is an independent project.
   Status: 1=Planning 2=Ongoing 3=Completed (Domain.Enums.FestivalStatus)
   Visibility: 1=Public 2=MembersOnly (Domain.Enums.FestivalVisibility)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.Festivals
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Festivals PRIMARY KEY,
    SocietyId        INT               NOT NULL,
    Name             NVARCHAR(150)     NOT NULL,
    Year             INT               NOT NULL,
    StartDate        DATETIME2         NOT NULL,
    EndDate          DATETIME2         NOT NULL,
    Description      NVARCHAR(2000)    NULL,
    BannerImageUrl   NVARCHAR(500)     NULL,
    CoverPhotoUrl    NVARCHAR(500)     NULL,
    Theme            NVARCHAR(100)     NULL,
    Status           TINYINT           NOT NULL CONSTRAINT DF_Festivals_Status DEFAULT (1),
    Visibility       TINYINT           NOT NULL CONSTRAINT DF_Festivals_Visibility DEFAULT (1),
    IsRecurring      BIT               NOT NULL CONSTRAINT DF_Festivals_IsRecurring DEFAULT (0),
    ParentFestivalId INT               NULL,
    CreatedAt        DATETIME2         NOT NULL CONSTRAINT DF_Festivals_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy        NVARCHAR(256)     NULL,
    ModifiedAt       DATETIME2         NULL,
    ModifiedBy       NVARCHAR(256)     NULL,
    IsDeleted        BIT               NOT NULL CONSTRAINT DF_Festivals_IsDeleted DEFAULT (0),
    DeletedAt        DATETIME2         NULL,
    DeletedBy        NVARCHAR(256)     NULL,
    CONSTRAINT FK_Festivals_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id),
    CONSTRAINT FK_Festivals_ParentFestival FOREIGN KEY (ParentFestivalId) REFERENCES dbo.Festivals(Id)
);
GO
CREATE INDEX IX_Festivals_SocietyId_Year ON dbo.Festivals(SocietyId, Year);
CREATE INDEX IX_Festivals_Status ON dbo.Festivals(Status);
GO

/* ---------------------------------------------------------------------------
   Budget: category lines + revision history.
   Category: 1=Decoration 2=Lighting 3=Sound 4=Food 5=Idol 6=Pandal 7=Stage
   8=Security 9=Cleaning 10=Generator 11=Photography 12=Miscellaneous
   (Domain.Enums.FestivalBudgetCategoryType). ActualAmount is deliberately NOT
   stored — always computed from FestivalExpenses so it can never drift.
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FestivalBudgetCategories
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FestivalBudgetCategories PRIMARY KEY,
    FestivalId       INT               NOT NULL,
    Category         TINYINT           NOT NULL,
    EstimatedAmount  DECIMAL(12,2)     NOT NULL CONSTRAINT DF_FestivalBudgetCategories_Estimated DEFAULT (0),
    ApprovedAmount   DECIMAL(12,2)     NOT NULL CONSTRAINT DF_FestivalBudgetCategories_Approved DEFAULT (0),
    Notes            NVARCHAR(500)     NULL,
    CreatedAt        DATETIME2         NOT NULL CONSTRAINT DF_FestivalBudgetCategories_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy        NVARCHAR(256)     NULL,
    ModifiedAt       DATETIME2         NULL,
    ModifiedBy       NVARCHAR(256)     NULL,
    IsDeleted        BIT               NOT NULL CONSTRAINT DF_FestivalBudgetCategories_IsDeleted DEFAULT (0),
    DeletedAt        DATETIME2         NULL,
    DeletedBy        NVARCHAR(256)     NULL,
    CONSTRAINT FK_FestivalBudgetCategories_Festivals FOREIGN KEY (FestivalId) REFERENCES dbo.Festivals(Id)
);
GO
CREATE UNIQUE INDEX UX_FestivalBudgetCategories_Festival_Category
    ON dbo.FestivalBudgetCategories(FestivalId, Category) WHERE IsDeleted = 0;
GO

CREATE TABLE dbo.FestivalBudgetRevisions
(
    Id                        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FestivalBudgetRevisions PRIMARY KEY,
    FestivalBudgetCategoryId  INT               NOT NULL,
    PreviousEstimatedAmount   DECIMAL(12,2)     NOT NULL,
    NewEstimatedAmount        DECIMAL(12,2)     NOT NULL,
    PreviousApprovedAmount    DECIMAL(12,2)     NOT NULL,
    NewApprovedAmount         DECIMAL(12,2)     NOT NULL,
    Reason                    NVARCHAR(500)     NULL,
    CreatedAt                 DATETIME2         NOT NULL CONSTRAINT DF_FestivalBudgetRevisions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy                 NVARCHAR(256)     NULL,
    ModifiedAt                DATETIME2         NULL,
    ModifiedBy                NVARCHAR(256)     NULL,
    IsDeleted                 BIT               NOT NULL CONSTRAINT DF_FestivalBudgetRevisions_IsDeleted DEFAULT (0),
    DeletedAt                 DATETIME2         NULL,
    DeletedBy                 NVARCHAR(256)     NULL,
    CONSTRAINT FK_FestivalBudgetRevisions_Categories FOREIGN KEY (FestivalBudgetCategoryId)
        REFERENCES dbo.FestivalBudgetCategories(Id)
);
GO
CREATE INDEX IX_FestivalBudgetRevisions_CategoryId ON dbo.FestivalBudgetRevisions(FestivalBudgetCategoryId);
GO

/* ---------------------------------------------------------------------------
   Member Contributions (donations).
   PaymentMethod: 1=Cash 2=UPI 3=BankTransfer (Domain.Enums.ContributionPaymentMethod)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FestivalContributions
(
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FestivalContributions PRIMARY KEY,
    FestivalId     INT               NOT NULL,
    FlatId         INT               NULL,
    MemberName     NVARCHAR(150)     NOT NULL,
    Amount         DECIMAL(12,2)     NOT NULL,
    PaymentMethod  TINYINT           NOT NULL,
    PaymentDate    DATETIME2         NOT NULL,
    TransactionId  NVARCHAR(100)     NULL,
    ReceiptNumber  NVARCHAR(30)      NOT NULL,
    IsAnonymous    BIT               NOT NULL CONSTRAINT DF_FestivalContributions_IsAnonymous DEFAULT (0),
    CreatedAt      DATETIME2         NOT NULL CONSTRAINT DF_FestivalContributions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy      NVARCHAR(256)     NULL,
    ModifiedAt     DATETIME2         NULL,
    ModifiedBy     NVARCHAR(256)     NULL,
    IsDeleted      BIT               NOT NULL CONSTRAINT DF_FestivalContributions_IsDeleted DEFAULT (0),
    DeletedAt      DATETIME2         NULL,
    DeletedBy      NVARCHAR(256)     NULL,
    CONSTRAINT FK_FestivalContributions_Festivals FOREIGN KEY (FestivalId) REFERENCES dbo.Festivals(Id),
    CONSTRAINT FK_FestivalContributions_Flats FOREIGN KEY (FlatId) REFERENCES dbo.Flats(Id) ON DELETE SET NULL
);
GO
CREATE UNIQUE INDEX UX_FestivalContributions_ReceiptNumber ON dbo.FestivalContributions(ReceiptNumber);
CREATE INDEX IX_FestivalContributions_FestivalId ON dbo.FestivalContributions(FestivalId);
GO

/* ---------------------------------------------------------------------------
   Sponsors.
   SponsorshipType: 1=Title 2=Platinum 3=Gold 4=Silver 5=Bronze 6=InKind 7=Media
   (Domain.Enums.SponsorshipType)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FestivalSponsors
(
    Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FestivalSponsors PRIMARY KEY,
    FestivalId       INT               NOT NULL,
    CompanyName      NVARCHAR(200)     NOT NULL,
    ContactPerson    NVARCHAR(150)     NULL,
    Phone            NVARCHAR(20)      NULL,
    Email            NVARCHAR(256)     NULL,
    SponsorshipType  TINYINT           NOT NULL,
    PromisedAmount   DECIMAL(12,2)     NOT NULL CONSTRAINT DF_FestivalSponsors_Promised DEFAULT (0),
    ReceivedAmount   DECIMAL(12,2)     NOT NULL CONSTRAINT DF_FestivalSponsors_Received DEFAULT (0),
    LogoUrl          NVARCHAR(500)     NULL,
    BannerUrl        NVARCHAR(500)     NULL,
    CreatedAt        DATETIME2         NOT NULL CONSTRAINT DF_FestivalSponsors_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy        NVARCHAR(256)     NULL,
    ModifiedAt       DATETIME2         NULL,
    ModifiedBy       NVARCHAR(256)     NULL,
    IsDeleted        BIT               NOT NULL CONSTRAINT DF_FestivalSponsors_IsDeleted DEFAULT (0),
    DeletedAt        DATETIME2         NULL,
    DeletedBy        NVARCHAR(256)     NULL,
    CONSTRAINT FK_FestivalSponsors_Festivals FOREIGN KEY (FestivalId) REFERENCES dbo.Festivals(Id)
);
GO
CREATE INDEX IX_FestivalSponsors_FestivalId ON dbo.FestivalSponsors(FestivalId);
GO

/* ---------------------------------------------------------------------------
   Vendor directory — society-scoped, reused across festivals/years.
   Category: 1=Decorator 2=Sound 3=Catering 4=Electrician 5=TentHouse
   6=Generator 7=Photographer 8=Other (Domain.Enums.VendorCategory)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FestivalVendors
(
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FestivalVendors PRIMARY KEY,
    SocietyId   INT               NOT NULL,
    Name        NVARCHAR(200)     NOT NULL,
    Category    TINYINT           NOT NULL,
    Phone       NVARCHAR(20)      NULL,
    Email       NVARCHAR(256)     NULL,
    GstNumber   NVARCHAR(20)      NULL,
    Address     NVARCHAR(500)     NULL,
    Rating      DECIMAL(3,2)      NOT NULL CONSTRAINT DF_FestivalVendors_Rating DEFAULT (0),
    CreatedAt   DATETIME2         NOT NULL CONSTRAINT DF_FestivalVendors_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(256)     NULL,
    ModifiedAt  DATETIME2         NULL,
    ModifiedBy  NVARCHAR(256)     NULL,
    IsDeleted   BIT               NOT NULL CONSTRAINT DF_FestivalVendors_IsDeleted DEFAULT (0),
    DeletedAt   DATETIME2         NULL,
    DeletedBy   NVARCHAR(256)     NULL,
    CONSTRAINT FK_FestivalVendors_Societies FOREIGN KEY (SocietyId) REFERENCES dbo.Societies(Id)
);
GO
CREATE INDEX IX_FestivalVendors_SocietyId_Name ON dbo.FestivalVendors(SocietyId, Name);
GO

/* ---------------------------------------------------------------------------
   Expenses — approval workflow: Draft -> Pending -> Approved -> Paid,
   or Pending -> Rejected. ApprovalStatus: 1=Draft 2=Pending 3=Approved
   4=Rejected 5=Paid (Domain.Enums.ExpenseApprovalStatus)
   --------------------------------------------------------------------------- */
CREATE TABLE dbo.FestivalExpenses
(
    Id                        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FestivalExpenses PRIMARY KEY,
    FestivalId                INT               NOT NULL,
    FestivalBudgetCategoryId  INT               NOT NULL,
    VendorId                  INT               NULL,
    Amount                    DECIMAL(12,2)     NOT NULL,
    ExpenseDate               DATETIME2         NOT NULL,
    Description               NVARCHAR(500)     NULL,
    PaymentMethod             TINYINT           NOT NULL,
    BillImageUrl              NVARCHAR(500)     NULL,
    InvoiceNumber             NVARCHAR(100)     NULL,
    ApprovalStatus            TINYINT           NOT NULL CONSTRAINT DF_FestivalExpenses_ApprovalStatus DEFAULT (1),
    ApprovedByUserId          INT               NULL,
    ApprovedAt                DATETIME2         NULL,
    RejectionReason           NVARCHAR(500)     NULL,
    CreatedAt                 DATETIME2         NOT NULL CONSTRAINT DF_FestivalExpenses_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy                 NVARCHAR(256)     NULL,
    ModifiedAt                DATETIME2         NULL,
    ModifiedBy                NVARCHAR(256)     NULL,
    IsDeleted                 BIT               NOT NULL CONSTRAINT DF_FestivalExpenses_IsDeleted DEFAULT (0),
    DeletedAt                 DATETIME2         NULL,
    DeletedBy                 NVARCHAR(256)     NULL,
    CONSTRAINT FK_FestivalExpenses_Festivals FOREIGN KEY (FestivalId) REFERENCES dbo.Festivals(Id),
    CONSTRAINT FK_FestivalExpenses_Categories FOREIGN KEY (FestivalBudgetCategoryId)
        REFERENCES dbo.FestivalBudgetCategories(Id),
    CONSTRAINT FK_FestivalExpenses_Vendors FOREIGN KEY (VendorId) REFERENCES dbo.FestivalVendors(Id) ON DELETE SET NULL,
    CONSTRAINT FK_FestivalExpenses_ApprovedBy FOREIGN KEY (ApprovedByUserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
);
GO
CREATE INDEX IX_FestivalExpenses_Festival_Status ON dbo.FestivalExpenses(FestivalId, ApprovalStatus);
GO

PRINT 'Festival & Event Management (Phase 1 — Foundation) schema created successfully.';
