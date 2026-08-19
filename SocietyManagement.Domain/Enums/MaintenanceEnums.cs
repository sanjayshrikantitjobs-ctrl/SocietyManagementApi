namespace SocietyManagement.Domain.Enums;

/// <summary>How a MaintenanceCategory's amount is computed. PerSqFt multiplies
/// by Flat.AreaSqFt at bill time; OneTime is only included in a flat's first
/// bill ever.</summary>
public enum ChargeType
{
    Fixed = 1,
    PerSqFt = 2,
    OneTime = 3
}

public enum ChargeFrequency
{
    Monthly = 1,
    OneTime = 2
}

public enum FineStatus
{
    Pending = 1,
    Billed = 2,
    Waived = 3
}

public enum BillStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4
}

public enum PaymentMode
{
    Cash = 1,
    UPI = 2,
    BankTransfer = 3,
    Cheque = 4
}

/// <summary>What a MaintenanceBillItem line item originated from.</summary>
public enum BillItemType
{
    Category = 1,
    SpecialCharge = 2,
    Fine = 3
}
