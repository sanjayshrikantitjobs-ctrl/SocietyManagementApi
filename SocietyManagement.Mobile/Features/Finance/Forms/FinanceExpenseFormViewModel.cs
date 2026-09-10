using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Finance.Forms;

public record FinanceExpensePaymentMethodOption(string Label, ContributionPaymentMethod Value);

/// <summary>Add/edit a general (society-level) Expense row — the same
/// entity the web's Finance > Expenses "Add Expense" dialog creates.
/// Festival expenses aren't editable here; see FinanceViewModel's own
/// ShowExpenseActionsAsync doc comment.</summary>
public partial class FinanceExpenseFormViewModel : ObservableObject
{
    private readonly FinanceClient _client;

    public FinanceExpenseFormViewModel(FinanceClient client)
    {
        _client = client;
        selectedCategory = CategoryOptions[0];
        selectedPaymentMethod = PaymentMethodOptions[0];
    }

    public List<ExpenseCategory> CategoryOptions { get; } = new()
    {
        ExpenseCategory.VendorPayment, ExpenseCategory.StaffSalary, ExpenseCategory.Electricity,
        ExpenseCategory.Repairs, ExpenseCategory.Other
    };

    public List<FinanceExpensePaymentMethodOption> PaymentMethodOptions { get; } = new()
    {
        new("Cash", ContributionPaymentMethod.Cash), new("UPI", ContributionPaymentMethod.UPI),
        new("Bank Transfer", ContributionPaymentMethod.BankTransfer),
    };

    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private ExpenseCategory selectedCategory;
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime expenseDate = DateTime.Today;
    [ObservableProperty] private FinanceExpensePaymentMethodOption selectedPaymentMethod;
    [ObservableProperty] private string? paidTo;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    async partial void OnIdChanged(int value)
    {
        if (value > 0) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var response = await _client.ExpensesGET2Async(Id);
            var expense = response.Data;
            if (expense is null) return;

            SelectedCategory = expense.Category ?? CategoryOptions[0];
            Title = expense.Title ?? string.Empty;
            Amount = (decimal)(expense.Amount ?? 0);
            if (expense.ExpenseDate is DateTimeOffset date) ExpenseDate = date.Date;
            SelectedPaymentMethod = PaymentMethodOptions.FirstOrDefault(o => o.Value == expense.PaymentMethod) ?? PaymentMethodOptions[0];
            PaidTo = expense.PaidTo;
            OnPropertyChanged(nameof(IsEditMode));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load this expense ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title) || Amount <= 0)
        {
            ErrorMessage = "Enter a title and an amount greater than zero.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.ExpensesPUTAsync(Id, new UpdateGeneralExpenseCommand
                {
                    Id = Id, Category = SelectedCategory, Title = Title, Amount = (double)Amount,
                    ExpenseDate = ExpenseDate, PaymentMethod = SelectedPaymentMethod.Value, PaidTo = PaidTo, Notes = Notes
                });
            }
            else
            {
                await _client.ExpensesPOSTAsync(new CreateGeneralExpenseCommand
                {
                    SocietyId = SocietyId, Category = SelectedCategory, Title = Title, Amount = (double)Amount,
                    ExpenseDate = ExpenseDate, PaymentMethod = SelectedPaymentMethod.Value, PaidTo = PaidTo, Notes = Notes
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
