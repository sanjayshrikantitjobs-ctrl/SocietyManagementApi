namespace SocietyManagement.Domain.Enums;

public enum FestivalStatus
{
    Planning = 1,
    Ongoing = 2,
    Completed = 3
}

public enum FestivalVisibility
{
    Public = 1,
    MembersOnly = 2
}

/// <summary>Standalone (every festival today) vs a shared yearly
/// contribution Pool vs a Child festival that draws its funding from a
/// Pool. Deliberately separate from ParentFestivalId, which means
/// "cloned from last year's instance" — an unrelated concept.</summary>
public enum FestivalKind
{
    Standalone = 1,
    Pool = 2,
    Child = 3
}

public enum FestivalBudgetCategoryType
{
    Decoration = 1,
    Lighting = 2,
    Sound = 3,
    Food = 4,
    Idol = 5,
    Pandal = 6,
    Stage = 7,
    Security = 8,
    Cleaning = 9,
    Generator = 10,
    Photography = 11,
    Miscellaneous = 12
}

public enum ContributionPaymentMethod
{
    Cash = 1,
    UPI = 2,
    BankTransfer = 3
}

/// <summary>Tier/type of sponsorship a company offers a festival.</summary>
public enum SponsorshipType
{
    Title = 1,
    Platinum = 2,
    Gold = 3,
    Silver = 4,
    Bronze = 5,
    InKind = 6,
    Media = 7
}

/// <summary>State machine for FestivalExpense.ApprovalStatus:
/// Draft -> Pending -> Approved -> Paid, or Pending -> Rejected.</summary>
public enum ExpenseApprovalStatus
{
    Draft = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4,
    Paid = 5
}

public enum VendorCategory
{
    Decorator = 1,
    Sound = 2,
    Catering = 3,
    Electrician = 4,
    TentHouse = 5,
    Generator = 6,
    Photographer = 7,
    Other = 8
}

public enum FestivalTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3
}

/// <summary>Per-flat status against a FestivalFlatTarget, derived at query
/// time from TargetAmount vs the sum of that flat's FestivalContributions —
/// never stored, same "compute don't denormalize" convention as
/// Festival.Collected and FestivalBudgetCategory.ActualAmount.</summary>
public enum FlatContributionStatus
{
    NoTarget = 0,
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3
}
