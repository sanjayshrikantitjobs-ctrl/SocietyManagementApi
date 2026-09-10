using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Features.Finance.Forms;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Finance;

/// <summary>One filterable source option — Value null means "All", same
/// convention as MaintenanceViewModel's BillStatusOption.</summary>
public record FinanceSourceOption(string Label, FinanceSource? Value);
public record FinanceExpenseCategoryOption(string Label, ExpenseCategory? Value);

/// <summary>Mirrors the web's Finance module tab bar (finance-shell equivalent):
/// Overview/Income/Expenses/Outstanding/Ledger/Receipts/Reports. Every list
/// tab pulls from the same FinanceQueryHelpers rows the backend already
/// exposes (FinanceQueryHelpers.cs) — this is a thin, read-mostly client,
/// same as every other module here.</summary>
public partial class FinanceViewModel : ObservableObject
{
    private static readonly FinanceSourceOption[] IncomeSourceOptionsSeed =
    {
        new("All Sources", null),
        new("Maintenance", Api.Generated.FinanceSource.Maintenance),
        new("Festival", Api.Generated.FinanceSource.Festival),
        new("Water Tanker", Api.Generated.FinanceSource.WaterTanker),
    };

    private static readonly FinanceSourceOption[] ExpenseSourceOptionsSeed =
    {
        new("All Sources", null),
        new("General", Api.Generated.FinanceSource.GeneralExpense),
        new("Festival", Api.Generated.FinanceSource.Festival),
    };

    private static readonly FinanceSourceOption[] OutstandingSourceOptionsSeed =
    {
        new("All Sources", null),
        new("Maintenance", Api.Generated.FinanceSource.Maintenance),
        new("Festival", Api.Generated.FinanceSource.Festival),
        new("Water Tanker", Api.Generated.FinanceSource.WaterTanker),
    };

    private static readonly FinanceExpenseCategoryOption[] ExpenseCategoryOptionsSeed =
    {
        new("All Categories", null),
        new("Vendor Payment", ExpenseCategory.VendorPayment),
        new("Staff Salary", ExpenseCategory.StaffSalary),
        new("Electricity", ExpenseCategory.Electricity),
        new("Repairs", ExpenseCategory.Repairs),
        new("Other", ExpenseCategory.Other),
    };

    private readonly FinanceClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public FinanceViewModel(FinanceClient client, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private string selectedTab = "Overview";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<FinanceSourceOption> IncomeSourceOptions { get; } = new(IncomeSourceOptionsSeed);
    public ObservableCollection<FinanceSourceOption> ExpenseSourceOptions { get; } = new(ExpenseSourceOptionsSeed);
    public ObservableCollection<FinanceSourceOption> OutstandingSourceOptions { get; } = new(OutstandingSourceOptionsSeed);
    public ObservableCollection<FinanceExpenseCategoryOption> ExpenseCategoryOptions { get; } = new(ExpenseCategoryOptionsSeed);

    [RelayCommand]
    private async Task SelectTabAsync(string tab)
    {
        SelectedTab = tab;
        ErrorMessage = null;
        switch (tab)
        {
            case "Overview": await LoadOverviewAsync(); break;
            case "Income": await LoadIncomeAsync(); break;
            case "Expenses": await LoadExpensesAsync(); break;
            case "Outstanding": await LoadOutstandingAsync(); break;
            case "Ledger": await LoadLedgerAsync(); break;
            case "Receipts": await LoadReceiptsAsync(); break;
            case "Reports": await LoadReportAsync(); break;
        }
    }

    // ==================== Overview ====================

    [ObservableProperty] private FinanceOverviewDto? overview;
    [ObservableProperty] private Chart? monthlyTrendChart;
    [ObservableProperty] private Chart? incomeBySourceChart;
    [ObservableProperty] private Chart? expenseByCategoryChart;

    [RelayCommand]
    private async Task LoadOverviewAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.OverviewAsync(societyId);
            Overview = response.Data;

            MonthlyTrendChart = ChartFactory.BuildPairedBar(
                (Overview?.MonthlyTrend ?? new()).Select(p => (p.MonthLabel, p.Income ?? 0, p.Expense ?? 0)).ToList(),
                ChartFactory.ColorSuccess, ChartFactory.ColorDanger);

            IncomeBySourceChart = ChartFactory.BuildCategoryDonut(
                (Overview?.IncomeBySource ?? new()).Select(c => (c.Label, c.Amount ?? 0)).ToList());

            ExpenseByCategoryChart = ChartFactory.BuildCategoryBar(
                (Overview?.ExpenseByCategory ?? new()).Select(c => (c.Label, c.Amount ?? 0)).ToList());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the finance overview ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Income ====================

    [ObservableProperty] private ObservableCollection<FinanceIncomeRowDto> incomes = new();
    [ObservableProperty] private string incomeSearch = string.Empty;
    [ObservableProperty] private FinanceSourceOption selectedIncomeSource = IncomeSourceOptionsSeed[0];

    partial void OnSelectedIncomeSourceChanged(FinanceSourceOption value) => _ = LoadIncomeCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadIncomeAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.IncomeAsync(
                societyId, SelectedIncomeSource.Value, null, null,
                string.IsNullOrWhiteSpace(IncomeSearch) ? null : IncomeSearch, 1, 50);
            Incomes = new ObservableCollection<FinanceIncomeRowDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load income ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Expenses ====================

    [ObservableProperty] private ObservableCollection<FinanceExpenseRowDto> expenses = new();
    [ObservableProperty] private string expenseSearch = string.Empty;
    [ObservableProperty] private FinanceSourceOption selectedExpenseSource = ExpenseSourceOptionsSeed[0];
    [ObservableProperty] private FinanceExpenseCategoryOption selectedExpenseCategory = ExpenseCategoryOptionsSeed[0];

    partial void OnSelectedExpenseSourceChanged(FinanceSourceOption value) => _ = LoadExpensesCommand.ExecuteAsync(null);
    partial void OnSelectedExpenseCategoryChanged(FinanceExpenseCategoryOption value) => _ = LoadExpensesCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadExpensesAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.ExpensesGETAsync(
                societyId, SelectedExpenseSource.Value, SelectedExpenseCategory.Value, null, null,
                string.IsNullOrWhiteSpace(ExpenseSearch) ? null : ExpenseSearch, 1, 50);
            Expenses = new ObservableCollection<FinanceExpenseRowDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load expenses ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddExpenseAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(FinanceExpenseFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowExpenseActionsAsync(FinanceExpenseRowDto expense)
    {
        // Only general (society-level) expenses are editable here — Festival
        // expenses stay owned by the Festivals module (FinanceExpenseFeature.cs's
        // own doc comment: "read-only here; still edited from the Festivals module").
        if (Shell.Current is null || expense.Source != Api.Generated.FinanceSource.GeneralExpense || expense.Id is not int id) return;

        var action = await Shell.Current.DisplayActionSheet("Expense", "Cancel", null, "Edit", "Delete");
        switch (action)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(FinanceExpenseFormPage), new Dictionary<string, object> { ["expenseId"] = id });
                break;
            case "Delete":
                if (!await Shell.Current.DisplayAlert("Delete Expense", $"Delete this expense of ₹{expense.Amount:N0}?", "Delete", "Cancel")) return;
                await _client.ExpensesDELETEAsync(id);
                await LoadExpensesAsync();
                break;
        }
    }

    // ==================== Outstanding ====================

    [ObservableProperty] private ObservableCollection<FinanceOutstandingRowDto> outstandings = new();
    [ObservableProperty] private string outstandingSearch = string.Empty;
    [ObservableProperty] private FinanceSourceOption selectedOutstandingSource = OutstandingSourceOptionsSeed[0];

    partial void OnSelectedOutstandingSourceChanged(FinanceSourceOption value) => _ = LoadOutstandingCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadOutstandingAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.OutstandingAsync(
                societyId, SelectedOutstandingSource.Value,
                string.IsNullOrWhiteSpace(OutstandingSearch) ? null : OutstandingSearch, 1, 50);
            Outstandings = new ObservableCollection<FinanceOutstandingRowDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load outstanding dues ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Ledger ====================

    [ObservableProperty] private ObservableCollection<FinanceLedgerRowDto> ledgerEntries = new();
    [ObservableProperty] private decimal ledgerOpeningBalance;
    [ObservableProperty] private DateTime ledgerFromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime ledgerToDate = DateTime.Today;
    [ObservableProperty] private bool hasLedgerDateFilter;

    [RelayCommand]
    private void ApplyLedgerDateFilter()
    {
        HasLedgerDateFilter = true;
        _ = LoadLedgerCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearLedgerDateFilter()
    {
        HasLedgerDateFilter = false;
        _ = LoadLedgerCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadLedgerAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var from = HasLedgerDateFilter ? (DateTimeOffset?)LedgerFromDate : null;
            var to = HasLedgerDateFilter ? (DateTimeOffset?)LedgerToDate : null;
            var response = await _client.LedgerAsync(societyId, from, to, 1, 100);
            LedgerEntries = new ObservableCollection<FinanceLedgerRowDto>(response.Data?.Items ?? new());
            LedgerOpeningBalance = (decimal)(response.Data?.OpeningBalance ?? 0);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the ledger ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Receipts ====================
    // Reuses the Income rows — every one already carries a ReceiptNumber
    // (FinanceIncomeFeature.cs's own doc comment), so there's no separate
    // receipts query on the backend either.

    [ObservableProperty] private ObservableCollection<FinanceIncomeRowDto> receipts = new();
    [ObservableProperty] private string receiptSearch = string.Empty;

    [RelayCommand]
    private async Task LoadReceiptsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.IncomeAsync(
                societyId, null, null, null,
                string.IsNullOrWhiteSpace(ReceiptSearch) ? null : ReceiptSearch, 1, 50);
            Receipts = new ObservableCollection<FinanceIncomeRowDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load receipts ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadReceiptAsync(FinanceIncomeRowDto receipt)
    {
        if (receipt.Source is not { } source || receipt.Id is not int id) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var file = await _client.PdfAsync(source, id);
            await SaveAndShareAsync(file, $"{receipt.ReceiptNumber}.pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't download the receipt ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Reports ====================

    [ObservableProperty] private FinanceReportSummaryDto? reportSummary;
    [ObservableProperty] private DateTime reportFromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime reportToDate = DateTime.Today;
    [ObservableProperty] private bool hasReportDateFilter;

    [RelayCommand]
    private void ApplyReportDateFilter()
    {
        HasReportDateFilter = true;
        _ = LoadReportCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearReportDateFilter()
    {
        HasReportDateFilter = false;
        _ = LoadReportCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadReportAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var from = HasReportDateFilter ? (DateTimeOffset?)ReportFromDate : null;
            var to = HasReportDateFilter ? (DateTimeOffset?)ReportToDate : null;
            var response = await _client.SummaryAsync(societyId, from, to);
            ReportSummary = response.Data;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the report ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ExportReportPdfAsync() => ExportReportAsync(pdf: true);

    [RelayCommand]
    private Task ExportReportExcelAsync() => ExportReportAsync(pdf: false);

    private async Task ExportReportAsync(bool pdf)
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var from = HasReportDateFilter ? (DateTimeOffset?)ReportFromDate : null;
            var to = HasReportDateFilter ? (DateTimeOffset?)ReportToDate : null;
            var file = pdf ? await _client.Pdf2Async(societyId, from, to) : await _client.ExcelAsync(societyId, from, to);
            await SaveAndShareAsync(file, pdf ? "financial-report.pdf" : "financial-report.xlsx");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't export the report ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task SaveAndShareAsync(FileResponse file, string fileName)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        using (file)
        {
            await using var output = File.Create(path);
            await file.Stream.CopyToAsync(output);
        }
        await Share.Default.RequestAsync(new ShareFileRequest { Title = fileName, File = new ShareFile(path) });
    }
}
