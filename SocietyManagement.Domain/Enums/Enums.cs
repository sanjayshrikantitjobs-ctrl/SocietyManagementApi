namespace SocietyManagement.Domain.Enums;

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}

/// <summary>How a member relates to a flat.</summary>
public enum MemberType
{
    Owner = 1,
    Tenant = 2,
    FamilyMember = 3
}

public enum FlatStatus
{
    Vacant = 1,
    Occupied = 2,
    UnderMaintenance = 3
}

public enum FlatType
{
    OneRK = 1,
    OneBHK = 2,
    TwoBHK = 3,
    ThreeBHK = 4,
    FourBHK = 5,
    Duplex = 6,
    Penthouse = 7,
    Shop = 8,
    Office = 9
}

public enum ParkingType
{
    TwoWheeler = 1,
    FourWheeler = 2,
    Visitor = 3
}

public enum ParkingStatus
{
    Vacant = 1,
    Allocated = 2,
    Reserved = 3
}

/// <summary>Reason an OTP was issued; keeps the OtpVerifications table generic.</summary>
public enum OtpPurpose
{
    Login = 1,
    ForgotPassword = 2,
    Registration = 3,
    MobileVerification = 4,
    EmailVerification = 5
}

public enum AuditAction
{
    Login = 1,
    Logout = 2,
    Create = 3,
    Update = 4,
    Delete = 5,
    Approve = 6,
    Reject = 7,
    Payment = 8,
    PasswordChange = 9,
    PasswordReset = 10,
    AccountLocked = 11,
    AccountUnlocked = 12,
    Export = 13
}

/// <summary>Owner-episode vs Tenant-episode — the discriminator that lets
/// FlatOccupancy model both with one shared group/history mechanism.</summary>
public enum OccupancyType
{
    Owner = 1,
    Tenant = 2
}

/// <summary>How an OccupancyMember relates to the occupancy's primary
/// person (the primary owner or primary tenant).</summary>
public enum PersonRelationship
{
    Self = 1,
    Spouse = 2,
    Son = 3,
    Daughter = 4,
    Parent = 5,
    Grandparent = 6,
    Sibling = 7,
    Other = 8
}

/// <summary>Is this occupancy member currently physically staying at the
/// flat, independent of whether their OccupancyMember row has formally
/// ended (LeftDate).</summary>
public enum ResidentStatus
{
    Residing = 1,
    NotResiding = 2
}

public enum PoliceVerificationStatus
{
    Pending = 1,
    Done = 2
}

public enum StaffCategory
{
    Watchman = 1,
    Sweeper = 2,
    Gardener = 3,
    Electrician = 4,
    Plumber = 5,
    Other = 6
}

/// <summary>General operating-expense bucket for the Finance module.
/// Festival expenses are tracked separately (FestivalExpense, its own
/// FestivalBudgetCategoryType) and are not re-categorized under this enum
/// — Finance reads them alongside Expense rows rather than duplicating
/// them into this table.</summary>
public enum ExpenseCategory
{
    VendorPayment = 1,
    StaffSalary = 2,
    Electricity = 3,
    Repairs = 4,
    Other = 5
}

public enum ComplaintCategory
{
    Plumbing = 1,
    Electrical = 2,
    Housekeeping = 3,
    Security = 4,
    Parking = 5,
    Structural = 6,
    Noise = 7,
    LiftElevator = 8,
    WaterSupply = 9,
    Other = 10
}

public enum ComplaintPriority
{
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>Linear workflow, enforced by guard clauses in the command
/// handlers — the same shape as VisitorVisitStatus/ExpenseApprovalStatus.
/// Reopen sends a Resolved complaint back to Open (not a distinct
/// "Reopened" state) so the same Assign→InProgress→Resolve pipeline
/// handles the second pass.</summary>
public enum ComplaintStatus
{
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5
}

/// <summary>A bug/support ticket raised against the software itself by an
/// Admin or Member, resolved by Super Admin (the software vendor's own
/// role in this app) — a simpler pipeline than ComplaintStatus since there's
/// no assignment step.</summary>
public enum SupportTicketStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3
}
