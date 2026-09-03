using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Finance;

/// <summary>Where a Finance row came from. Purely a query/DTO tag — not
/// stored anywhere — since Income/Expense/Outstanding each pull from
/// multiple existing tables rather than one unified ledger table.</summary>
public enum FinanceSource
{
    Maintenance = 1,
    Festival = 2,
    WaterTanker = 3,
    GeneralExpense = 4
}

public class FinanceIncomeRowDto
{
    /// <summary>PK of the underlying MaintenancePayment/FestivalContribution/
    /// WaterTankerCollection row — combined with Source to build the receipt
    /// PDF route.</summary>
    public int Id { get; set; }
    public FinanceSource Source { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string ReceiptNumber { get; set; } = default!;
    public string PayerName { get; set; } = default!;
    public string? FlatNumber { get; set; }
    public string Description { get; set; } = default!;
}

public class FinanceExpenseRowDto
{
    /// <summary>PK of the underlying Expense/FestivalExpense row.</summary>
    public int Id { get; set; }
    public FinanceSource Source { get; set; }
    public string CategoryLabel { get; set; } = default!;
    public string Title { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? PaidTo { get; set; }
    public int? FestivalId { get; set; }
    public string? FestivalName { get; set; }
}

public class FinanceOutstandingRowDto
{
    public FinanceSource Source { get; set; }
    public string? FlatNumber { get; set; }
    public string PayerName { get; set; } = default!;
    public decimal Amount { get; set; }
    public int? DaysOverdue { get; set; }
}

/// <summary>Shared cross-source query building for the Finance module.
/// Every Finance page (Overview, Income, Ledger, Outstanding, Reports)
/// needs the same "pull matching rows from Maintenance + Festival +
/// WaterTanker (+ general Expenses)" logic — kept in one place instead of
/// repeating the same three EF queries in every handler. Data volume per
/// society is small enough that materializing full row sets and filtering/
/// aggregating in memory (rather than five separate SQL aggregate queries
/// per page) is simpler and consistent with how GetFlatContributionsQuery
/// already does its own in-memory per-flat join.</summary>
internal static class FinanceQueryHelpers
{
    internal static string ExpenseCategoryLabel(ExpenseCategory category) => category switch
    {
        ExpenseCategory.VendorPayment => "Vendor Payment",
        ExpenseCategory.StaffSalary => "Staff Salary",
        ExpenseCategory.Electricity => "Electricity",
        ExpenseCategory.Repairs => "Repairs",
        _ => "Other"
    };

    internal static string SourceLabel(FinanceSource source) => source switch
    {
        FinanceSource.Maintenance => "Maintenance",
        FinanceSource.Festival => "Festival",
        FinanceSource.WaterTanker => "Water Tanker",
        _ => "General"
    };

    public static async Task<List<FinanceIncomeRowDto>> GetIncomeRowsAsync(
        IApplicationDbContext context, int societyId, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        var maintenance = await context.MaintenancePayments
            .Where(p => !p.IsDeleted && p.MaintenanceBill.Flat.Floor.Wing.Building.SocietyId == societyId
                && (!dateFrom.HasValue || p.PaymentDate >= dateFrom) && (!dateTo.HasValue || p.PaymentDate <= dateTo))
            .Select(p => new FinanceIncomeRowDto
            {
                Id = p.Id,
                Source = FinanceSource.Maintenance,
                Date = p.PaymentDate,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMode.ToString(),
                ReceiptNumber = "MNT-" + p.Id,
                PayerName = p.MaintenanceBill.OwnerNameSnapshot ?? p.MaintenanceBill.Flat.FlatNumber,
                FlatNumber = p.MaintenanceBill.Flat.FlatNumber,
                Description = "Maintenance - " + p.MaintenanceBill.BillMonth.ToString("MMM yyyy")
            })
            .ToListAsync(ct);

        var festival = await context.FestivalContributions
            .Where(c => !c.IsDeleted && c.Festival.SocietyId == societyId
                && (!dateFrom.HasValue || c.PaymentDate >= dateFrom) && (!dateTo.HasValue || c.PaymentDate <= dateTo))
            .Select(c => new FinanceIncomeRowDto
            {
                Id = c.Id,
                Source = FinanceSource.Festival,
                Date = c.PaymentDate,
                Amount = c.Amount,
                PaymentMethod = c.PaymentMethod.ToString(),
                ReceiptNumber = c.ReceiptNumber,
                PayerName = c.MemberName,
                FlatNumber = c.Flat != null ? c.Flat.FlatNumber : null,
                Description = "Festival - " + c.Festival.Name
            })
            .ToListAsync(ct);

        var waterTanker = await context.WaterTankerCollections
            .Where(w => !w.IsDeleted && w.SocietyId == societyId && w.IsPaid && w.PaymentDate != null
                && (!dateFrom.HasValue || w.PaymentDate >= dateFrom) && (!dateTo.HasValue || w.PaymentDate <= dateTo))
            .Select(w => new FinanceIncomeRowDto
            {
                Id = w.Id,
                Source = FinanceSource.WaterTanker,
                Date = w.PaymentDate!.Value,
                Amount = w.Amount,
                PaymentMethod = null,
                ReceiptNumber = "WTR-" + w.Id,
                PayerName = w.Flat.FlatNumber,
                FlatNumber = w.Flat.FlatNumber,
                Description = "Water Tanker - " + w.Month.ToString("MMM yyyy")
            })
            .ToListAsync(ct);

        return maintenance.Concat(festival).Concat(waterTanker).ToList();
    }

    public static async Task<List<FinanceExpenseRowDto>> GetExpenseRowsAsync(
        IApplicationDbContext context, int societyId, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        // Category label mapping happens after materialization (not inside
        // the EF Select) — a custom C# method call there wouldn't translate
        // to SQL, and even the built-in .ToString() on an enum, while
        // translatable, still gives PascalCase identifiers ("StaffSalary")
        // rather than a display-friendly label.
        var generalRaw = await context.Expenses
            .Where(e => !e.IsDeleted && e.SocietyId == societyId
                && (!dateFrom.HasValue || e.ExpenseDate >= dateFrom) && (!dateTo.HasValue || e.ExpenseDate <= dateTo))
            .Select(e => new
            {
                e.Id, e.Category, e.Title, e.Amount, e.ExpenseDate, e.PaymentMethod, e.PaidTo,
                StaffName = e.Staff != null ? e.Staff.FirstName + " " + e.Staff.LastName : null
            })
            .ToListAsync(ct);

        var general = generalRaw.Select(e => new FinanceExpenseRowDto
        {
            Id = e.Id,
            Source = FinanceSource.GeneralExpense,
            CategoryLabel = ExpenseCategoryLabel(e.Category),
            Title = e.Title,
            Amount = e.Amount,
            ExpenseDate = e.ExpenseDate,
            PaymentMethod = e.PaymentMethod.ToString(),
            PaidTo = e.PaidTo ?? e.StaffName,
            FestivalId = null,
            FestivalName = null
        }).ToList();

        var festival = await context.FestivalExpenses
            .Where(e => !e.IsDeleted && e.Festival.SocietyId == societyId
                && (e.ApprovalStatus == ExpenseApprovalStatus.Approved || e.ApprovalStatus == ExpenseApprovalStatus.Paid)
                && (!dateFrom.HasValue || e.ExpenseDate >= dateFrom) && (!dateTo.HasValue || e.ExpenseDate <= dateTo))
            .Select(e => new FinanceExpenseRowDto
            {
                Id = e.Id,
                Source = FinanceSource.Festival,
                CategoryLabel = "Festival: " + e.FestivalBudgetCategory.Category.ToString(),
                Title = e.Description ?? e.FestivalBudgetCategory.Category.ToString(),
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                PaymentMethod = e.PaymentMethod.ToString(),
                PaidTo = e.Vendor != null ? e.Vendor.Name : null,
                FestivalId = e.FestivalId,
                FestivalName = e.Festival.Name
            })
            .ToListAsync(ct);

        return general.Concat(festival).ToList();
    }

    public static async Task<List<FinanceOutstandingRowDto>> GetOutstandingRowsAsync(
        IApplicationDbContext context, int societyId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var unpaidBills = await context.MaintenanceBills
            .Where(b => !b.IsDeleted && !b.IsRolledForward && b.Status != BillStatus.Paid && b.Flat.Floor.Wing.Building.SocietyId == societyId)
            .Select(b => new
            {
                FlatNumber = b.Flat.FlatNumber, PayerName = b.OwnerNameSnapshot ?? b.Flat.FlatNumber,
                Balance = b.TotalAmount - b.AmountPaid, b.DueDate
            })
            .ToListAsync(ct);

        var maintenanceRows = unpaidBills.Select(b => new FinanceOutstandingRowDto
        {
            Source = FinanceSource.Maintenance, FlatNumber = b.FlatNumber, PayerName = b.PayerName, Amount = b.Balance,
            DaysOverdue = b.DueDate < today ? (today - b.DueDate.Date).Days : null
        });

        var unpaidTanker = await context.WaterTankerCollections
            .Where(w => !w.IsDeleted && w.SocietyId == societyId && !w.IsPaid)
            .Select(w => new { FlatNumber = w.Flat.FlatNumber, w.Amount })
            .ToListAsync(ct);

        var waterTankerRows = unpaidTanker.Select(w => new FinanceOutstandingRowDto
        {
            Source = FinanceSource.WaterTanker, FlatNumber = w.FlatNumber, PayerName = w.FlatNumber, Amount = w.Amount,
            DaysOverdue = null
        });

        // Festival outstanding only exists where an admin has set a
        // FestivalFlatTarget — most festivals collect voluntary
        // contributions with no fixed target, so nothing is "owed" there.
        var targets = await context.FestivalFlatTargets
            .Where(t => t.Festival.SocietyId == societyId && !t.Festival.IsDeleted)
            .Select(t => new { t.FestivalId, t.FlatId, FlatNumber = t.Flat.FlatNumber, t.TargetAmount })
            .ToListAsync(ct);

        var festivalRows = new List<FinanceOutstandingRowDto>();
        if (targets.Count > 0)
        {
            var festivalIds = targets.Select(t => t.FestivalId).Distinct().ToList();
            var paidByFlat = (await context.FestivalContributions
                .Where(c => festivalIds.Contains(c.FestivalId) && c.FlatId != null)
                .GroupBy(c => new { c.FestivalId, FlatId = c.FlatId!.Value })
                .Select(g => new { g.Key.FestivalId, g.Key.FlatId, Paid = g.Sum(c => c.Amount) })
                .ToListAsync(ct))
                .ToDictionary(x => (x.FestivalId, x.FlatId), x => x.Paid);

            festivalRows = targets
                .Select(t => new
                {
                    t.FlatNumber,
                    Outstanding = t.TargetAmount - paidByFlat.GetValueOrDefault((t.FestivalId, t.FlatId), 0)
                })
                .Where(t => t.Outstanding > 0)
                .Select(t => new FinanceOutstandingRowDto
                {
                    Source = FinanceSource.Festival, FlatNumber = t.FlatNumber, PayerName = t.FlatNumber,
                    Amount = t.Outstanding, DaysOverdue = null
                })
                .ToList();
        }

        return maintenanceRows.Concat(waterTankerRows).Concat(festivalRows).ToList();
    }
}
