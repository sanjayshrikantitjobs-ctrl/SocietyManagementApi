namespace SocietyManagement.Domain.Enums;

/// <summary>Valid transitions:
/// PendingApproval -> Approved -> CheckedIn -> CheckedOut
/// PendingApproval -> Rejected
/// PendingApproval -> Expired    (background job)
/// PendingApproval -> Cancelled  (watchman, visitor left before a decision)
/// No other transition is allowed — e.g. CheckedOut is terminal.</summary>
public enum VisitorVisitStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    CheckedIn = 4,
    CheckedOut = 5,
    Expired = 6,
    Cancelled = 7
}
