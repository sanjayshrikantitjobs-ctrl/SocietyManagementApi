using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Features.Festivals.Forms;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile.Features.Festivals;

/// <summary>One filterable status option for the Contribution tab's "By
/// Flat" status picker — Value null means "All", mirroring the web's
/// [value]="null" option.</summary>
public record ContributionStatusOption(string Label, FlatContributionStatus? Value);

/// <summary>Mirrors festival-detail.component.ts's full tab set: Dashboard,
/// Child Festivals &amp; Events (Pool kind only), Contribution (Standalone/Pool),
/// Budget/Sponsors/Expenses/Vendors/Volunteers/Tasks (Standalone/Child kind) —
/// same tab-visibility rule keyed off Festival.Kind. Each tab's data loads
/// lazily on first selection (SelectTabAsync), same as the web's mat-tab-group
/// only rendering the active tab's component.</summary>
public partial class FestivalDetailViewModel : ObservableObject
{
    private readonly FestivalsClient _festivalsClient;
    private readonly FestivalDashboardClient _dashboardClient;
    private readonly FestivalBudgetCategoriesClient _budgetClient;
    private readonly FestivalContributionsClient _contributionsClient;
    private readonly FestivalSponsorsClient _sponsorsClient;
    private readonly FestivalExpensesClient _expensesClient;
    private readonly FestivalVendorsClient _vendorsClient;
    private readonly FestivalVolunteersClient _volunteersClient;
    private readonly FestivalTasksClient _tasksClient;

    public FestivalDetailViewModel(
        FestivalsClient festivalsClient, FestivalDashboardClient dashboardClient,
        FestivalBudgetCategoriesClient budgetClient, FestivalContributionsClient contributionsClient,
        FestivalSponsorsClient sponsorsClient, FestivalExpensesClient expensesClient,
        FestivalVendorsClient vendorsClient, FestivalVolunteersClient volunteersClient, FestivalTasksClient tasksClient)
    {
        _festivalsClient = festivalsClient;
        _dashboardClient = dashboardClient;
        _budgetClient = budgetClient;
        _contributionsClient = contributionsClient;
        _sponsorsClient = sponsorsClient;
        _expensesClient = expensesClient;
        _vendorsClient = vendorsClient;
        _volunteersClient = volunteersClient;
        _tasksClient = tasksClient;
    }

    [ObservableProperty] private int festivalId;
    [ObservableProperty] private FestivalDto? festival;
    [ObservableProperty] private FestivalKpisDto? kpis;
    [ObservableProperty] private Chart? collectionProgressChart;
    [ObservableProperty] private Chart? budgetVsActualChart;
    [ObservableProperty] private Chart? expenseByCategoryChart;
    [ObservableProperty] private Chart? sponsorContributionChart;
    [ObservableProperty] private ObservableCollection<PoolChildSummaryDto> childFestivals = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isStatusMenuOpen;
    [ObservableProperty] private string selectedTab = "Dashboard";

    public FestivalStatus[] StatusOptions { get; } = { FestivalStatus.Planning, FestivalStatus.Ongoing, FestivalStatus.Completed };

    public bool IsPool => Festival?.Kind == FestivalKind.Pool;
    public bool IsChild => Festival?.Kind == FestivalKind.Child;
    public bool ShowContributionTab => !IsChild;
    public bool ShowChildFestivalsTab => IsPool;
    public bool ShowStandardTabs => !IsPool;

    async partial void OnFestivalIdChanged(int value) => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (FestivalId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var festivalResponse = await _festivalsClient.FestivalsGET2Async(FestivalId);
            Festival = festivalResponse.Data;
            NotifyKindFlagsChanged();

            var dashboardResponse = await _dashboardClient.FestivalDashboardAsync(FestivalId);
            var dashboard = dashboardResponse.Data;
            Kpis = dashboard?.Kpis;
            BuildDashboardCharts(dashboard);

            if (Festival?.Kind == FestivalKind.Pool)
            {
                var poolSummary = await _festivalsClient.PoolSummaryAsync(FestivalId);
                ChildFestivals = new ObservableCollection<PoolChildSummaryDto>(poolSummary.Data?.Children ?? new());
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load this festival ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Mirrors festival-dashboard's 4 charts (Budget vs Actual,
    /// Collection Progress, Expense by Category, Sponsor Contribution) —
    /// FestivalDashboardDto already carries every point these need
    /// (FestivalDashboardFeature.cs), just never rendered on mobile before.</summary>
    private void BuildDashboardCharts(FestivalDashboardDto? dashboard)
    {
        if (dashboard is null)
        {
            CollectionProgressChart = null;
            BudgetVsActualChart = null;
            ExpenseByCategoryChart = null;
            SponsorContributionChart = null;
            return;
        }

        var budget = dashboard.Kpis?.Budget ?? 0;
        var collected = dashboard.Kpis?.Collected ?? 0;
        var remaining = Math.Max(budget - collected, 0);
        CollectionProgressChart = ChartFactory.BuildProportionDonut(
            "Collected", collected, ChartFactory.ColorSuccess,
            "Remaining", remaining, ChartFactory.ColorMuted);

        BudgetVsActualChart = ChartFactory.BuildPairedBar(
            (dashboard.BudgetVsActual ?? new()).Select(c => (c.CategoryName, c.Approved ?? 0, c.Actual ?? 0)).ToList(),
            ChartFactory.ColorInfo, ChartFactory.ColorWarning);

        ExpenseByCategoryChart = ChartFactory.BuildCategoryDonut(
            (dashboard.ExpenseByCategory ?? new()).Select(e => (e.CategoryName, e.Amount ?? 0)).ToList());

        SponsorContributionChart = ChartFactory.BuildPairedBar(
            (dashboard.SponsorContributions ?? new()).Select(s => (s.CompanyName, s.Promised ?? 0, s.Received ?? 0)).ToList(),
            ChartFactory.ColorInfo, ChartFactory.ColorSuccess);
    }

    private void NotifyKindFlagsChanged()
    {
        OnPropertyChanged(nameof(IsPool));
        OnPropertyChanged(nameof(IsChild));
        OnPropertyChanged(nameof(ShowContributionTab));
        OnPropertyChanged(nameof(ShowChildFestivalsTab));
        OnPropertyChanged(nameof(ShowStandardTabs));
    }

    [RelayCommand]
    private async Task OpenChildFestivalAsync(PoolChildSummaryDto child)
    {
        if (child.FestivalId is int id)
            await Shell.Current.GoToAsync($"{nameof(FestivalDetailPage)}?festivalId={id}");
    }

    [RelayCommand]
    private void ToggleStatusMenu() => IsStatusMenuOpen = !IsStatusMenuOpen;

    [RelayCommand]
    private async Task SetStatusAsync(FestivalStatus status)
    {
        IsStatusMenuOpen = false;
        try
        {
            await _festivalsClient.StatusAsync(FestivalId, status);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update the status ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        IsStatusMenuOpen = false;
        try
        {
            await _festivalsClient.FestivalsDELETEAsync(FestivalId);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete this festival ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task SelectTabAsync(string tab)
    {
        SelectedTab = tab;
        ErrorMessage = null;
        switch (tab)
        {
            case "Dashboard": await LoadAsync(); break;
            case "Contribution": await LoadContributionAsync(); break;
            case "Budget": await LoadBudgetAsync(); break;
            case "Sponsors": await LoadSponsorsAsync(); break;
            case "Expenses": await LoadExpensesAsync(); break;
            case "Vendors": await LoadVendorsAsync(); break;
            case "Volunteers": await LoadVolunteersAsync(); break;
            case "Tasks": await LoadTasksAsync(); break;
        }
    }

    // ==================== Budget ====================

    [ObservableProperty] private ObservableCollection<FestivalBudgetCategoryDto> budgetCategories = new();

    public double TotalEstimated => BudgetCategories.Sum(c => c.EstimatedAmount ?? 0);
    public double TotalApproved => BudgetCategories.Sum(c => c.ApprovedAmount ?? 0);
    public double TotalActual => BudgetCategories.Sum(c => c.ActualAmount ?? 0);

    [RelayCommand]
    private async Task LoadBudgetAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _budgetClient.FestivalBudgetCategoriesGETAsync(FestivalId);
            BudgetCategories = new ObservableCollection<FestivalBudgetCategoryDto>(response.Data ?? new());
            OnPropertyChanged(nameof(TotalEstimated));
            OnPropertyChanged(nameof(TotalApproved));
            OnPropertyChanged(nameof(TotalActual));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the budget ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddBudgetCategoryAsync()
    {
        await Shell.Current.GoToAsync(nameof(BudgetCategoryFormPage), new Dictionary<string, object> { ["festivalId"] = FestivalId });
    }

    [RelayCommand]
    private async Task ShowBudgetCategoryActionsAsync(FestivalBudgetCategoryDto category)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(
            category.Category == FestivalBudgetCategoryType.Custom ? category.CustomCategoryName : category.Category?.ToString(),
            "Cancel", null, "Edit", "View History", "Delete");

        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(BudgetCategoryFormPage),
                    new Dictionary<string, object> { ["festivalId"] = FestivalId, ["category"] = category });
                break;
            case "View History":
                await Shell.Current.GoToAsync(nameof(BudgetRevisionsPage),
                    new Dictionary<string, object> { ["categoryId"] = category.Id ?? 0, ["categoryName"] = category.CustomCategoryName ?? category.Category?.ToString() ?? "Category" });
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Category",
                    $"Delete the \"{(category.Category == FestivalBudgetCategoryType.Custom ? category.CustomCategoryName : category.Category?.ToString())}\" budget category?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _budgetClient.FestivalBudgetCategoriesDELETEAsync(category.Id ?? 0);
                    await LoadBudgetAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the category ({ex.Message}).";
                }
                break;
        }
    }

    // ==================== Sponsors ====================

    [ObservableProperty] private ObservableCollection<FestivalSponsorDto> sponsors = new();

    [RelayCommand]
    private async Task LoadSponsorsAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _sponsorsClient.FestivalSponsorsGETAsync(FestivalId);
            Sponsors = new ObservableCollection<FestivalSponsorDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load sponsors ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddSponsorAsync()
    {
        await Shell.Current.GoToAsync(nameof(SponsorFormPage), new Dictionary<string, object> { ["festivalId"] = FestivalId });
    }

    [RelayCommand]
    private async Task ShowSponsorActionsAsync(FestivalSponsorDto sponsor)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(sponsor.CompanyName, "Cancel", null, "Edit", "Remove");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(SponsorFormPage),
                    new Dictionary<string, object> { ["festivalId"] = FestivalId, ["sponsor"] = sponsor });
                break;
            case "Remove":
                var confirmed = await Shell.Current.DisplayAlert("Remove Sponsor", $"Remove \"{sponsor.CompanyName}\" as a sponsor?", "Remove", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _sponsorsClient.FestivalSponsorsDELETEAsync(sponsor.Id ?? 0);
                    await LoadSponsorsAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't remove the sponsor ({ex.Message}).";
                }
                break;
        }
    }

    // ==================== Vendors (society-scoped) ====================

    [ObservableProperty] private ObservableCollection<FestivalVendorDto> vendors = new();
    [ObservableProperty] private string vendorSearch = string.Empty;

    [RelayCommand]
    private async Task LoadVendorsAsync()
    {
        if (Festival?.SocietyId is not int societyId) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _vendorsClient.FestivalVendorsGETAsync(societyId, null, string.IsNullOrWhiteSpace(VendorSearch) ? null : VendorSearch, 1, 100);
            Vendors = new ObservableCollection<FestivalVendorDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load vendors ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddVendorAsync()
    {
        if (Festival?.SocietyId is not int societyId) return;
        await Shell.Current.GoToAsync(nameof(VendorFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowVendorActionsAsync(FestivalVendorDto vendor)
    {
        if (Shell.Current is null || Festival?.SocietyId is not int societyId) return;

        var choice = await Shell.Current.DisplayActionSheet(vendor.Name, "Cancel", null, "Edit", "Delete");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(VendorFormPage),
                    new Dictionary<string, object> { ["societyId"] = societyId, ["vendor"] = vendor });
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Vendor", $"Delete \"{vendor.Name}\" from the vendor directory?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _vendorsClient.FestivalVendorsDELETEAsync(vendor.Id ?? 0);
                    await LoadVendorsAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the vendor ({ex.Message}).";
                }
                break;
        }
    }

    // ==================== Volunteers ====================

    [ObservableProperty] private ObservableCollection<FestivalVolunteerDto> volunteers = new();

    [RelayCommand]
    private async Task LoadVolunteersAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _volunteersClient.FestivalVolunteersGETAsync(FestivalId);
            Volunteers = new ObservableCollection<FestivalVolunteerDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load volunteers ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddVolunteerAsync()
    {
        await Shell.Current.GoToAsync(nameof(VolunteerFormPage), new Dictionary<string, object> { ["festivalId"] = FestivalId });
    }

    [RelayCommand]
    private async Task ShowVolunteerActionsAsync(FestivalVolunteerDto volunteer)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(volunteer.Name, "Cancel", null, "Edit", "Remove");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(VolunteerFormPage),
                    new Dictionary<string, object> { ["festivalId"] = FestivalId, ["volunteer"] = volunteer });
                break;
            case "Remove":
                var confirmed = await Shell.Current.DisplayAlert("Remove Volunteer", $"Remove \"{volunteer.Name}\" as a volunteer?", "Remove", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _volunteersClient.FestivalVolunteersDELETEAsync(volunteer.Id ?? 0);
                    await LoadVolunteersAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't remove the volunteer ({ex.Message}).";
                }
                break;
        }
    }

    // ==================== Tasks ====================

    [ObservableProperty] private ObservableCollection<FestivalTaskDto> tasks = new();

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _tasksClient.FestivalTasksGETAsync(FestivalId);
            Tasks = new ObservableCollection<FestivalTaskDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load tasks ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        await Shell.Current.GoToAsync(nameof(TaskFormPage), new Dictionary<string, object> { ["festivalId"] = FestivalId });
    }

    [RelayCommand]
    private async Task ShowTaskActionsAsync(FestivalTaskDto task)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(task.Title, "Cancel", null, "Edit", "Delete");
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(TaskFormPage),
                    new Dictionary<string, object> { ["festivalId"] = FestivalId, ["task"] = task });
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Task", $"Delete \"{task.Title}\"?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _tasksClient.FestivalTasksDELETEAsync(task.Id ?? 0);
                    await LoadTasksAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the task ({ex.Message}).";
                }
                break;
        }
    }

    // ==================== Expenses ====================

    [ObservableProperty] private ObservableCollection<FestivalExpenseDto> expenses = new();

    [RelayCommand]
    private async Task LoadExpensesAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _expensesClient.FestivalExpensesGETAsync(FestivalId, null, null, 1, 100);
            Expenses = new ObservableCollection<FestivalExpenseDto>(response.Data?.Items ?? new());
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
        if (Festival?.SocietyId is not int societyId) return;
        await Shell.Current.GoToAsync(nameof(ExpenseFormPage),
            new Dictionary<string, object> { ["festivalId"] = FestivalId, ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowExpenseActionsAsync(FestivalExpenseDto expense)
    {
        if (Shell.Current is null || Festival?.SocietyId is not int societyId) return;

        var editable = expense.ApprovalStatus is ExpenseApprovalStatus.Draft or ExpenseApprovalStatus.Rejected;
        var options = new List<string>();
        if (editable) { options.Add("Edit"); options.Add("Delete"); }
        if (expense.ApprovalStatus == ExpenseApprovalStatus.Draft) options.Add("Submit for Approval");
        if (expense.ApprovalStatus == ExpenseApprovalStatus.Pending) { options.Add("Approve"); options.Add("Reject"); }
        if (expense.ApprovalStatus == ExpenseApprovalStatus.Approved) options.Add("Mark as Paid");

        if (options.Count == 0) return;
        var choice = await Shell.Current.DisplayActionSheet($"Expense — ₹{expense.Amount:N0}", "Cancel", null, options.ToArray());

        try
        {
            switch (choice)
            {
                case "Edit":
                    await Shell.Current.GoToAsync(nameof(ExpenseFormPage),
                        new Dictionary<string, object> { ["festivalId"] = FestivalId, ["societyId"] = societyId, ["expense"] = expense });
                    break;
                case "Delete":
                    if (!await Shell.Current.DisplayAlert("Delete Expense", $"Delete this expense of ₹{expense.Amount:N0}?", "Delete", "Cancel")) return;
                    await _expensesClient.FestivalExpensesDELETEAsync(expense.Id ?? 0);
                    await LoadExpensesAsync();
                    break;
                case "Submit for Approval":
                    await _expensesClient.SubmitAsync(expense.Id ?? 0);
                    await LoadExpensesAsync();
                    break;
                case "Approve":
                    await _expensesClient.ApprovePOSTAsync(expense.Id ?? 0);
                    await LoadExpensesAsync();
                    break;
                case "Reject":
                    var reason = await Shell.Current.DisplayPromptAsync("Reject Expense", "Reason for rejection", "Reject", "Cancel");
                    if (string.IsNullOrWhiteSpace(reason)) return;
                    await _expensesClient.RejectPOSTAsync(expense.Id ?? 0, new RejectExpenseRequest { Reason = reason });
                    await LoadExpensesAsync();
                    break;
                case "Mark as Paid":
                    await _expensesClient.MarkPaidAsync(expense.Id ?? 0);
                    await LoadExpensesAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update the expense ({ex.Message}).";
        }
    }

    // ==================== Contribution ====================

    private static readonly ContributionStatusOption[] ContributionStatusOptionsSeed =
    {
        new("All Statuses", null),
        new("No Target", FlatContributionStatus.NoTarget),
        new("Pending", FlatContributionStatus.Pending),
        new("Partially Paid", FlatContributionStatus.PartiallyPaid),
        new("Paid", FlatContributionStatus.Paid),
    };

    public ObservableCollection<ContributionStatusOption> ContributionStatusOptions { get; } = new(ContributionStatusOptionsSeed);

    [ObservableProperty] private string contributionView = "Flats";
    [ObservableProperty] private FlatContributionKpisDto? contributionKpis;
    [ObservableProperty] private ObservableCollection<FlatContributionDto> flatContributions = new();
    [ObservableProperty] private ObservableCollection<FestivalContributionDto> allContributions = new();
    [ObservableProperty] private string contributionSearch = string.Empty;
    [ObservableProperty] private ContributionStatusOption selectedContributionStatus = ContributionStatusOptionsSeed[0];

    partial void OnSelectedContributionStatusChanged(ContributionStatusOption value) => _ = LoadContributionCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadContributionAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var kpisResponse = await _contributionsClient.Kpis2Async(FestivalId);
            ContributionKpis = kpisResponse.Data;

            var search = string.IsNullOrWhiteSpace(ContributionSearch) ? null : ContributionSearch;
            if (ContributionView == "Flats")
            {
                var statuses = SelectedContributionStatus.Value is { } status
                    ? new List<FlatContributionStatus> { status }
                    : null;
                var response = await _contributionsClient.FlatSummaryAsync(FestivalId, search, statuses, null, false, 1, 100);
                FlatContributions = new ObservableCollection<FlatContributionDto>(response.Data?.Items ?? new());
            }
            else
            {
                var response = await _contributionsClient.FestivalContributionsGETAsync(FestivalId, null, search, null, null, false, 1, 100);
                AllContributions = new ObservableCollection<FestivalContributionDto>(response.Data?.Items ?? new());
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load contributions ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SwitchContributionViewAsync(string view)
    {
        ContributionView = view;
        await LoadContributionAsync();
    }

    [RelayCommand]
    private async Task SetTargetForAllAsync()
    {
        if (Shell.Current is null) return;

        var input = await Shell.Current.DisplayPromptAsync("Set Target for All Flats", "Annual Target per Flat (₹)", "Set", "Cancel", initialValue: "0", keyboard: Keyboard.Numeric);
        if (input is null || !double.TryParse(input, out var amount)) return;

        IsBusy = true;
        try
        {
            var response = await _contributionsClient.TargetsPOSTAsync(new SetContributionTargetsCommand { FestivalId = FestivalId, TargetAmount = amount });
            await Shell.Current.DisplayAlert("Set Target", $"Target set for {response.Data} flat(s).", "OK");
            await LoadContributionAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't set targets ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RecordContributionAsync()
    {
        await Shell.Current.GoToAsync(nameof(ContributionFormPage), new Dictionary<string, object> { ["festivalId"] = FestivalId });
    }

    [RelayCommand]
    private async Task OpenFlatDetailAsync(FlatContributionDto flat)
    {
        await Shell.Current.GoToAsync(nameof(FlatContributionDetailPage),
            new Dictionary<string, object> { ["festivalId"] = FestivalId, ["flatId"] = flat.FlatId ?? 0, ["flatNumber"] = flat.FlatNumber ?? string.Empty });
    }

    [RelayCommand]
    private async Task ShowContributionActionsAsync(FestivalContributionDto contribution)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(contribution.ReceiptNumber, "Cancel", null, "Download Receipt", "Resend WhatsApp", "Edit");
        try
        {
            switch (choice)
            {
                case "Download Receipt":
                    var file = await _contributionsClient.ReceiptAsync(contribution.Id ?? 0);
                    var path = Path.Combine(FileSystem.CacheDirectory, $"receipt-{contribution.ReceiptNumber}.pdf");
                    using (file) { await using var output = File.Create(path); await file.Stream.CopyToAsync(output); }
                    await Share.Default.RequestAsync(new ShareFileRequest { Title = contribution.ReceiptNumber, File = new ShareFile(path) });
                    break;
                case "Resend WhatsApp":
                    var number = await Shell.Current.DisplayPromptAsync("Resend to WhatsApp", "Mobile number", "Send", "Cancel", initialValue: contribution.WhatsAppNumber, keyboard: Keyboard.Numeric);
                    if (number is null) return;
                    await _contributionsClient.ResendWhatsappAsync(contribution.Id ?? 0, new ResendWhatsAppRequest { WhatsAppNumber = string.IsNullOrWhiteSpace(number) ? null : number });
                    await Shell.Current.DisplayAlert("Resend WhatsApp", "Receipt resent.", "OK");
                    break;
                case "Edit":
                    await Shell.Current.GoToAsync(nameof(ContributionFormPage),
                        new Dictionary<string, object> { ["festivalId"] = FestivalId, ["contribution"] = contribution });
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't complete that action ({ex.Message}).";
        }
    }
}
