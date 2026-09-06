using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public record ExpenseCategoryOption(string Label, int Value);
public record ExpenseVendorOption(string Label, int? Value);
public record ExpensePaymentModeOption(string Label, ContributionPaymentMethod Value);

/// <summary>Mirrors expense-form-dialog.component.ts. Bill photo upload is a
/// separate multipart flow on web (FileUploadService) — not wired up on
/// this pass, so BillImageUrl is a plain URL field here rather than a
/// camera/file picker; the field itself still round-trips to the backend
/// unchanged.</summary>
public partial class ExpenseFormViewModel : ObservableObject
{
    private readonly FestivalExpensesClient _client;
    private readonly FestivalBudgetCategoriesClient _budgetClient;
    private readonly FestivalVendorsClient _vendorsClient;

    private static readonly ExpenseVendorOption NoneVendorOption = new("— None —", null);

    public ExpenseFormViewModel(FestivalExpensesClient client, FestivalBudgetCategoriesClient budgetClient, FestivalVendorsClient vendorsClient)
    {
        _client = client;
        _budgetClient = budgetClient;
        _vendorsClient = vendorsClient;
        selectedVendor = NoneVendorOption;
        selectedPaymentMode = PaymentModeOptions[0];
    }

    public List<ExpensePaymentModeOption> PaymentModeOptions { get; } = new()
    {
        new("Cash", ContributionPaymentMethod.Cash), new("UPI", ContributionPaymentMethod.UPI), new("Bank Transfer", ContributionPaymentMethod.BankTransfer),
    };

    [ObservableProperty] private List<ExpenseCategoryOption> categoryOptions = new();
    [ObservableProperty] private List<ExpenseVendorOption> vendorOptions = new() { NoneVendorOption };
    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private ExpenseCategoryOption? selectedCategory;
    [ObservableProperty] private ExpenseVendorOption selectedVendor;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime expenseDate = DateTime.Today;
    [ObservableProperty] private ExpensePaymentModeOption selectedPaymentMode;
    [ObservableProperty] private string? invoiceNumber;
    [ObservableProperty] private string? description;
    [ObservableProperty] private string? billImageUrl;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    private int? _pendingCategoryId;
    private int? _pendingVendorId;

    async partial void OnFestivalIdChanged(int value) => await LoadCategoriesAsync();
    async partial void OnSocietyIdChanged(int value) => await LoadVendorsAsync();

    private async Task LoadCategoriesAsync()
    {
        if (FestivalId <= 0) return;
        try
        {
            var response = await _budgetClient.FestivalBudgetCategoriesGETAsync(FestivalId);
            CategoryOptions = (response.Data ?? new())
                .Select(c => new ExpenseCategoryOption(
                    c.Category == FestivalBudgetCategoryType.Custom ? c.CustomCategoryName ?? "Custom" : c.Category?.ToString() ?? "—", c.Id ?? 0))
                .ToList();
            if (_pendingCategoryId is int pending)
                SelectedCategory = CategoryOptions.FirstOrDefault(o => o.Value == pending);
            else if (SelectedCategory is null)
                SelectedCategory = CategoryOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load budget categories ({ex.Message}).";
        }
    }

    private async Task LoadVendorsAsync()
    {
        if (SocietyId <= 0) return;
        try
        {
            var response = await _vendorsClient.FestivalVendorsGETAsync(SocietyId, null, null, 1, 100);
            var options = new List<ExpenseVendorOption> { NoneVendorOption };
            options.AddRange((response.Data?.Items ?? new()).Select(v => new ExpenseVendorOption(v.Name ?? "—", v.Id)));
            VendorOptions = options;
            if (_pendingVendorId is int pending)
                SelectedVendor = VendorOptions.FirstOrDefault(o => o.Value == pending) ?? NoneVendorOption;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load vendors ({ex.Message}).";
        }
    }

    public void LoadFrom(FestivalExpenseDto expense)
    {
        Id = expense.Id ?? 0;
        Amount = (decimal)(expense.Amount ?? 0);
        if (expense.ExpenseDate is DateTimeOffset date) ExpenseDate = date.Date;
        SelectedPaymentMode = PaymentModeOptions.FirstOrDefault(o => o.Value == expense.PaymentMethod) ?? PaymentModeOptions[0];
        InvoiceNumber = expense.InvoiceNumber;
        Description = expense.Description;
        BillImageUrl = expense.BillImageUrl;
        _pendingCategoryId = expense.FestivalBudgetCategoryId;
        _pendingVendorId = expense.VendorId;
        SelectedCategory = CategoryOptions.FirstOrDefault(o => o.Value == expense.FestivalBudgetCategoryId);
        SelectedVendor = VendorOptions.FirstOrDefault(o => o.Value == expense.VendorId) ?? NoneVendorOption;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (SelectedCategory is null || Amount <= 0)
        {
            ErrorMessage = "Pick a budget category and enter an amount greater than zero.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.FestivalExpensesPUTAsync(Id, new UpdateExpenseCommand
                {
                    Id = Id, FestivalBudgetCategoryId = SelectedCategory.Value, VendorId = SelectedVendor.Value,
                    Amount = (double)Amount, ExpenseDate = ExpenseDate, Description = Description,
                    PaymentMethod = SelectedPaymentMode.Value, BillImageUrl = BillImageUrl, InvoiceNumber = InvoiceNumber
                });
            }
            else
            {
                await _client.FestivalExpensesPOSTAsync(new CreateExpenseCommand
                {
                    FestivalId = FestivalId, FestivalBudgetCategoryId = SelectedCategory.Value, VendorId = SelectedVendor.Value,
                    Amount = (double)Amount, ExpenseDate = ExpenseDate, Description = Description,
                    PaymentMethod = SelectedPaymentMode.Value, BillImageUrl = BillImageUrl, InvoiceNumber = InvoiceNumber
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the expense ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
