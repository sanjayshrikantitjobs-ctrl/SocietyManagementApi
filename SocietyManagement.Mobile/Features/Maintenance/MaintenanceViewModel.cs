using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Features.Maintenance.Forms;
using SocietyManagement.Mobile.Features.Maintenance.Payments;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Maintenance;

/// <summary>One filterable status option for the Bills tab's Status picker
/// — Value null means "All", mirroring the web's [value]="null" option in
/// maintenance-bills-list.component.ts.</summary>
public record BillStatusOption(string Label, BillStatus? Value);

public record FineStatusOption(string Label, FineStatus? Value);

/// <summary>Mirrors the web's Maintenance module tab bar (maintenance-shell.component.ts):
/// Dashboard/Bills/Categories/Special Charges/Fines/Water Tanker/Settings.
/// Dashboard, Bills and Categories are real; the remaining four are a
/// follow-up (same "Coming soon" stance as the nav's placeholder modules)
/// so the tab structure itself already matches the web app.</summary>
public partial class MaintenanceViewModel : ObservableObject
{
    private static readonly BillStatusOption[] StatusOptionsSeed =
    {
        new("All Statuses", null),
        new("Pending", Api.Generated.BillStatus.Pending),
        new("Partially Paid", Api.Generated.BillStatus.PartiallyPaid),
        new("Paid", Api.Generated.BillStatus.Paid),
        new("Overdue", Api.Generated.BillStatus.Overdue),
    };

    private readonly MaintenanceDashboardClient _dashboardClient;
    private readonly MaintenanceBillsClient _billsClient;
    private readonly MaintenanceCategoriesClient _categoriesClient;
    private readonly SpecialChargesClient _specialChargesClient;
    private readonly FineRecordsClient _finesClient;
    private readonly WaterTankerLogsClient _waterTankerClient;
    private readonly MaintenanceSettingsClient _settingsClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public MaintenanceViewModel(
        MaintenanceDashboardClient dashboardClient, MaintenanceBillsClient billsClient,
        MaintenanceCategoriesClient categoriesClient, SpecialChargesClient specialChargesClient,
        FineRecordsClient finesClient, WaterTankerLogsClient waterTankerClient, MaintenanceSettingsClient settingsClient,
        CurrentSocietyService currentSocietyService)
    {
        _dashboardClient = dashboardClient;
        _billsClient = billsClient;
        _categoriesClient = categoriesClient;
        _specialChargesClient = specialChargesClient;
        _finesClient = finesClient;
        _waterTankerClient = waterTankerClient;
        _settingsClient = settingsClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private string selectedTab = "Dashboard";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [ObservableProperty] private MaintenanceKpisDto? kpis;
    [ObservableProperty] private Chart? monthlyTrendChart;
    [ObservableProperty] private Chart? paidVsPendingChart;
    [ObservableProperty] private Chart? outstandingByWingChart;
    [ObservableProperty] private ObservableCollection<MaintenanceBillDto> bills = new();
    [ObservableProperty] private ObservableCollection<MaintenanceCategoryDto> categories = new();

    /// <summary>"Month" or "Year" — mirrors the web dashboard's
    /// Monthly/Yearly mat-button-toggle-group.</summary>
    [ObservableProperty] private string dashboardViewMode = "Month";
    [ObservableProperty] private DateTime dashboardMonthDate = DateTime.Today;
    [ObservableProperty] private int dashboardYear = DateTime.Today.Year;
    public ObservableCollection<int> DashboardYearOptions { get; } = new(BuildDashboardYearOptions());

    private static List<int> BuildDashboardYearOptions()
    {
        var current = DateTime.Today.Year;
        return Enumerable.Range(0, 6).Select(i => current + 1 - i).ToList();
    }

    partial void OnDashboardMonthDateChanged(DateTime value) => _ = LoadDashboardCommand.ExecuteAsync(null);
    partial void OnDashboardYearChanged(int value) => _ = LoadDashboardCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task SetDashboardViewModeAsync(string mode)
    {
        DashboardViewMode = mode;
        await LoadDashboardAsync();
    }

    public ObservableCollection<BillStatusOption> StatusOptions { get; } = new(StatusOptionsSeed);

    [ObservableProperty] private BillStatusOption selectedStatusOption = StatusOptionsSeed[0];
    [ObservableProperty] private DateTime billMonthFilterDate = DateTime.Today;
    [ObservableProperty] private bool hasMonthFilter = true;
    [ObservableProperty] private string billSearch = string.Empty;
    [ObservableProperty] private BillsBalanceSummaryDto? billsBalanceSummary;

    partial void OnSelectedStatusOptionChanged(BillStatusOption value) => _ = LoadBillsCommand.ExecuteAsync(null);

    partial void OnBillMonthFilterDateChanged(DateTime value)
    {
        HasMonthFilter = true;
        _ = LoadBillsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearMonthFilter()
    {
        HasMonthFilter = false;
        _ = LoadBillsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task SelectTabAsync(string tab)
    {
        SelectedTab = tab;
        ErrorMessage = null;
        switch (tab)
        {
            case "Dashboard": await LoadDashboardAsync(); break;
            case "Bills": await LoadBillsAsync(); break;
            case "Categories": await LoadCategoriesAsync(); break;
            case "SpecialCharges": await LoadSpecialChargesAsync(); break;
            case "Fines": await LoadFinesAsync(); break;
            case "WaterTanker": await LoadWaterTankerAsync(); break;
            case "Settings": await LoadSettingsAsync(); break;
        }
    }

    [RelayCommand]
    private async Task LoadDashboardAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        try
        {
            DateTimeOffset? month = DashboardViewMode == "Month" ? DashboardMonthDate : null;
            int? year = DashboardViewMode == "Year" ? DashboardYear : null;
            var response = await _dashboardClient.DashboardAsync(societyId, month, year);
            var dashboard = response.Data;
            Kpis = dashboard?.Kpis;

            MonthlyTrendChart = ChartFactory.BuildTrendLine(
                (dashboard?.MonthlyCollectionTrend ?? new()).Select(p => (p.MonthLabel, p.Amount ?? 0)).ToList(),
                ChartFactory.ColorInfo);

            var paid = dashboard?.PaidVsPending?.PaidAmount ?? 0;
            var outstanding = dashboard?.PaidVsPending?.OutstandingAmount ?? 0;
            PaidVsPendingChart = ChartFactory.BuildProportionDonut(
                "Paid", paid, ChartFactory.ColorSuccess,
                "Outstanding", outstanding, ChartFactory.ColorMuted);

            OutstandingByWingChart = ChartFactory.BuildCategoryBar(
                (dashboard?.OutstandingByWing ?? new()).Select(w => (w.WingName, w.Outstanding ?? 0)).ToList());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the dashboard ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadBillsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var statuses = SelectedStatusOption.Value is { } status
                ? new List<BillStatus> { status }
                : null;
            var billMonth = HasMonthFilter ? BillMonthFilterDate : (DateTime?)null;
            var search = string.IsNullOrWhiteSpace(BillSearch) ? null : BillSearch;

            var response = await _billsClient.BillsAsync(societyId, null, statuses, billMonth, search, 1, 50);
            Bills = new ObservableCollection<MaintenanceBillDto>(response.Data?.Items ?? new());

            var summaryResponse = await _billsClient.BalanceSummaryAsync(societyId, null, statuses, billMonth, search);
            BillsBalanceSummary = summaryResponse.Data;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load bills ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadCategoriesAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        try
        {
            var response = await _categoriesClient.MaintenanceCategoriesGETAsync(societyId);
            Categories = new ObservableCollection<MaintenanceCategoryDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load categories ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateBillsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }
        if (Shell.Current is null) return;

        var generateMonth = HasMonthFilter ? BillMonthFilterDate : DateTime.Today;
        var confirmed = await Shell.Current.DisplayAlert(
            "Generate Bills", $"Generate bills for {generateMonth:MMMM yyyy}?", "Generate", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _billsClient.Generate2Async(new GenerateBillsRequest
            {
                SocietyId = societyId,
                BillMonth = generateMonth
            });
            await Shell.Current.DisplayAlert("Generate Bills", $"{response.Data} bill(s) generated.", "OK");
            await LoadBillsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't generate bills ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ExportBillsPdfAsync() => ExportBillsAsync(pdf: true);

    [RelayCommand]
    private Task ExportBillsExcelAsync() => ExportBillsAsync(pdf: false);

    private async Task ExportBillsAsync(bool pdf)
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var statuses = SelectedStatusOption.Value is { } status
                ? new List<BillStatus> { status }
                : null;
            var billMonth = HasMonthFilter ? (DateTimeOffset?)BillMonthFilterDate : null;

            var file = pdf
                ? await _billsClient.Pdf5Async(societyId, statuses, billMonth)
                : await _billsClient.Excel3Async(societyId, statuses, billMonth);
            await SaveAndShareAsync(file, pdf ? "maintenance-bills.pdf" : "maintenance-bills.xlsx");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't export bills ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ShowBillActionsAsync(MaintenanceBillDto bill)
    {
        if (Shell.Current is null || bill.Id is not int billId) return;

        var options = new List<string> { "View Detail", "Download PDF" };
        var canRecordPayment = bill.Status != Api.Generated.BillStatus.Paid && bill.IsRolledForward != true;
        var canMarkUnpaid = bill.Status != Api.Generated.BillStatus.Pending;
        if (canRecordPayment) options.Add("Record Payment");
        if (canMarkUnpaid) options.Add("Mark as Unpaid");
        options.Add("Resend WhatsApp");

        var choice = await Shell.Current.DisplayActionSheet(bill.InvoiceNumber, "Cancel", null, options.ToArray());

        switch (choice)
        {
            case "View Detail":
                await Shell.Current.GoToAsync($"{nameof(MaintenanceBillDetailPage)}?billId={billId}");
                break;
            case "Download PDF":
                await DownloadBillPdfAsync(bill);
                break;
            case "Record Payment":
                await Shell.Current.GoToAsync(
                    $"{nameof(RecordPaymentPage)}?billId={billId}&invoiceNumber={Uri.EscapeDataString(bill.InvoiceNumber ?? string.Empty)}&balance={bill.Balance ?? 0}");
                break;
            case "Mark as Unpaid":
                await MarkUnpaidAsync(bill);
                break;
            case "Resend WhatsApp":
                await ResendWhatsAppAsync(billId);
                break;
        }
    }

    private async Task DownloadBillPdfAsync(MaintenanceBillDto bill)
    {
        if (bill.Id is not int billId) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var file = await _billsClient.Pdf4Async(billId);
            await SaveAndShareAsync(file, $"{bill.InvoiceNumber}.pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't download the PDF ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MarkUnpaidAsync(MaintenanceBillDto bill)
    {
        if (Shell.Current is null || bill.Id is not int billId) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "Mark as Unpaid", $"Reverse payment on {bill.InvoiceNumber}? Any payments recorded against this bill will be voided.",
            "Mark as Unpaid", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _billsClient.MarkUnpaidAsync(billId);
            await LoadBillsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't mark the bill unpaid ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResendWhatsAppAsync(int billId)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _billsClient.ResendWhatsapp2Async(billId);
            if (Shell.Current is not null)
                await Shell.Current.DisplayAlert("Resend WhatsApp", "Bill resent via WhatsApp.", "OK");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't resend the bill ({ex.Message}).";
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

    // ==================== Special Charges ====================

    [ObservableProperty] private ObservableCollection<SpecialChargeDto> specialCharges = new();
    [ObservableProperty] private string specialChargeSearch = string.Empty;

    [RelayCommand]
    private async Task LoadSpecialChargesAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _specialChargesClient.SpecialChargesGETAsync(
                societyId, null, string.IsNullOrWhiteSpace(SpecialChargeSearch) ? null : SpecialChargeSearch, null, false, 1, 100);
            SpecialCharges = new ObservableCollection<SpecialChargeDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load special charges ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddSpecialChargeAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        await Shell.Current.GoToAsync(nameof(SpecialChargeFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowSpecialChargeActionsAsync(SpecialChargeDto charge)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(charge.ChargeName, "Cancel", null, "Edit", "Delete");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(SpecialChargeFormPage), new Dictionary<string, object> { ["charge"] = charge });
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Special Charge", $"Delete the \"{charge.ChargeName}\" special charge?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _specialChargesClient.SpecialChargesDELETEAsync(charge.Id ?? 0);
                    await LoadSpecialChargesAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the special charge ({ex.Message}).";
                }
                break;
        }
    }

    // ==================== Fines ====================

    private static readonly FineStatusOption[] FineStatusOptionsSeed =
    {
        new("All Statuses", null),
        new("Pending", Api.Generated.FineStatus.Pending),
        new("Billed", Api.Generated.FineStatus.Billed),
        new("Waived", Api.Generated.FineStatus.Waived),
    };

    public ObservableCollection<FineStatusOption> FineStatusOptions { get; } = new(FineStatusOptionsSeed);

    [ObservableProperty] private ObservableCollection<FineRecordDto> fines = new();
    [ObservableProperty] private string fineSearch = string.Empty;
    [ObservableProperty] private FineStatusOption selectedFineStatus = FineStatusOptionsSeed[0];

    partial void OnSelectedFineStatusChanged(FineStatusOption value) => _ = LoadFinesCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadFinesAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _finesClient.FineRecordsGETAsync(
                societyId, null, SelectedFineStatus.Value, string.IsNullOrWhiteSpace(FineSearch) ? null : FineSearch, null, false, 1, 100);
            Fines = new ObservableCollection<FineRecordDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load fines ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddFineAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        await Shell.Current.GoToAsync(nameof(FineFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowFineActionsAsync(FineRecordDto fine)
    {
        if (Shell.Current is null || fine.Status != Api.Generated.FineStatus.Pending) return;

        var choice = await Shell.Current.DisplayActionSheet(fine.Reason, "Cancel", null, "Waive", "Delete");
        try
        {
            switch (choice)
            {
                case "Waive":
                    if (!await Shell.Current.DisplayAlert("Waive Fine", $"Waive this fine of ₹{fine.Amount:N0}?", "Waive", "Cancel")) return;
                    await _finesClient.WaiveAsync(fine.Id ?? 0);
                    await LoadFinesAsync();
                    break;
                case "Delete":
                    if (!await Shell.Current.DisplayAlert("Delete Fine", $"Delete this fine of ₹{fine.Amount:N0}?", "Delete", "Cancel")) return;
                    await _finesClient.FineRecordsDELETEAsync(fine.Id ?? 0);
                    await LoadFinesAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update the fine ({ex.Message}).";
        }
    }

    // ==================== Water Tanker (log-based) ====================

    [ObservableProperty] private ObservableCollection<WaterTankerLogDto> waterTankerLogs = new();
    [ObservableProperty] private WaterTankerLogMonthSummaryDto? waterTankerSummary;
    [ObservableProperty] private string waterTankerSearch = string.Empty;
    [ObservableProperty] private DateTime waterTankerMonth = DateTime.Today;

    partial void OnWaterTankerMonthChanged(DateTime value) => _ = LoadWaterTankerCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadWaterTankerAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var summaryResponse = await _waterTankerClient.Summary4Async(societyId, WaterTankerMonth);
            WaterTankerSummary = summaryResponse.Data;

            var response = await _waterTankerClient.WaterTankerLogsGETAsync(
                societyId, WaterTankerMonth, string.IsNullOrWhiteSpace(WaterTankerSearch) ? null : WaterTankerSearch, 1, 100);
            WaterTankerLogs = new ObservableCollection<WaterTankerLogDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load water tanker logs ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddWaterTankerLogAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        await Shell.Current.GoToAsync(nameof(WaterTankerLogFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowWaterTankerLogActionsAsync(WaterTankerLogDto log)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(log.ProviderName, "Cancel", null, "Edit", "Delete");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(WaterTankerLogFormPage), new Dictionary<string, object> { ["log"] = log });
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Entry", $"Delete this tanker entry from {log.ProviderName}?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _waterTankerClient.WaterTankerLogsDELETEAsync(log.Id ?? 0);
                    await LoadWaterTankerAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the entry ({ex.Message}).";
                }
                break;
        }
    }

    // ==================== Settings ====================

    [ObservableProperty] private int settingsBillGenerationDay = 1;
    [ObservableProperty] private int settingsDueDay = 5;
    [ObservableProperty] private int settingsGracePeriodDays = 5;
    [ObservableProperty] private decimal settingsLateFeeAmount;
    [ObservableProperty] private string settingsInvoiceNumberPrefix = string.Empty;
    [ObservableProperty] private bool settingsWhatsAppEnabled;
    [ObservableProperty] private string settingsWhatsAppMessageTemplate = string.Empty;
    [ObservableProperty] private string settingsPdfFooterMessage = string.Empty;

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _settingsClient.MaintenanceSettingsGETAsync(societyId);
            var settings = response.Data;
            if (settings is not null)
            {
                SettingsBillGenerationDay = settings.BillGenerationDay ?? 1;
                SettingsDueDay = settings.DueDay ?? 5;
                SettingsGracePeriodDays = settings.GracePeriodDays ?? 5;
                SettingsLateFeeAmount = (decimal)(settings.LateFeeAmount ?? 0);
                SettingsInvoiceNumberPrefix = settings.InvoiceNumberPrefix ?? string.Empty;
                SettingsWhatsAppEnabled = settings.WhatsAppEnabled ?? false;
                SettingsWhatsAppMessageTemplate = settings.WhatsAppMessageTemplate ?? string.Empty;
                SettingsPdfFooterMessage = settings.PdfFooterMessage ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load settings ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _settingsClient.MaintenanceSettingsPUTAsync(new UpsertMaintenanceSettingsCommand
            {
                SocietyId = societyId, BillGenerationDay = SettingsBillGenerationDay, DueDay = SettingsDueDay,
                GracePeriodDays = SettingsGracePeriodDays, LateFeeAmount = (double)SettingsLateFeeAmount,
                InvoiceNumberPrefix = SettingsInvoiceNumberPrefix, WhatsAppMessageTemplate = SettingsWhatsAppMessageTemplate,
                PdfFooterMessage = SettingsPdfFooterMessage, WhatsAppEnabled = SettingsWhatsAppEnabled
            });
            if (Shell.Current is not null) await Shell.Current.DisplayAlert("Settings", "Settings saved.", "OK");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save settings ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
