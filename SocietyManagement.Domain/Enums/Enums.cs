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
