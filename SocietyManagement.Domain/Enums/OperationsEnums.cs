namespace SocietyManagement.Domain.Enums;

public enum PetType
{
    Dog = 1,
    Cat = 2,
    Bird = 3,
    Fish = 4,
    Other = 5
}

public enum AttendanceStatus
{
    Present = 1,
    Late = 2,
    HalfDay = 3,
    Absent = 4,
    Leave = 5
}

public enum SocietyDocumentCategory
{
    Bylaws = 1,
    Policy = 2,
    Circular = 3,
    MeetingMinutes = 4,
    Certificate = 5,
    Contract = 6,
    Financial = 7,
    Other = 8
}

/// <summary>Who may open a society document — enforced server-side in
/// SocietyDocumentFeature, not just hidden in the UI.</summary>
public enum DocumentVisibility
{
    AdminOnly = 1,
    AllResidents = 2
}

public enum ServiceVendorCategory
{
    Electrical = 1,
    Plumbing = 2,
    Security = 3,
    Housekeeping = 4,
    Lift = 5,
    Garden = 6,
    PestControl = 7,
    Other = 8
}

public enum StockTransactionType
{
    In = 1,
    Out = 2,
    Adjustment = 3
}

public enum PurchasePriority
{
    Low = 1,
    Normal = 2,
    High = 3
}

public enum PurchaseStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Ordered = 5,
    PartiallyReceived = 6,
    Received = 7,
    Cancelled = 8
}
