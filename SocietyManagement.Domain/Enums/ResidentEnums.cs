namespace SocietyManagement.Domain.Enums;

public enum VehicleType
{
    TwoWheeler = 1,
    FourWheeler = 2
}

/// <summary>Lifecycle of a FlatResaleListing.</summary>
public enum ListingStatus
{
    Available = 1,
    UnderNegotiation = 2,
    Sold = 3,
    Withdrawn = 4
}
