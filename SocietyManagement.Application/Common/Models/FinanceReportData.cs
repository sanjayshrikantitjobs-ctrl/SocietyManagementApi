namespace SocietyManagement.Application.Common.Models;

public record FinanceReportLine(string Label, decimal Amount);

/// <summary>Everything IFinanceReportService needs to render the Financial
/// Reports PDF/Excel export — the same aggregate shape as the Overview
/// page, just date-range-scoped instead of all-time.</summary>
public record FinanceReportData(
    string SocietyName,
    string? SocietyLogoUrl,
    DateTime? DateFrom,
    DateTime? DateTo,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetBalance,
    List<FinanceReportLine> IncomeBySource,
    List<FinanceReportLine> ExpenseByCategory);
