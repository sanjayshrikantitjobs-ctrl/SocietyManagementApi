using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Maintenance.Payments;

/// <summary>Mirrors the web's Record Payment prompt dialog (used from both
/// maintenance-bills-list.component.ts and maintenance-bill-detail.component.ts) —
/// a dedicated page here since MAUI has no equivalent generic modal-form
/// dialog component to reuse.</summary>
public record PaymentModeOption(string Label, PaymentMode Value);

public partial class RecordPaymentViewModel : ObservableObject
{
    private static readonly PaymentModeOption[] PaymentModeOptionsSeed =
    {
        new("Cash", PaymentMode.Cash),
        new("UPI", PaymentMode.UPI),
        new("Bank Transfer", PaymentMode.BankTransfer),
        new("Cheque", PaymentMode.Cheque),
    };

    private readonly MaintenanceBillsClient _billsClient;

    public RecordPaymentViewModel(MaintenanceBillsClient billsClient)
    {
        _billsClient = billsClient;
    }

    public List<PaymentModeOption> PaymentModeOptions { get; } = new(PaymentModeOptionsSeed);

    [ObservableProperty] private int billId;
    [ObservableProperty] private string invoiceNumber = string.Empty;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime paymentDate = DateTime.Today;
    [ObservableProperty] private PaymentModeOption selectedPaymentMode = PaymentModeOptionsSeed[0];
    [ObservableProperty] private string? transactionReference;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public void SetBalance(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var balance))
        {
            Amount = balance;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (Amount <= 0)
        {
            ErrorMessage = "Enter a payment amount greater than zero.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _billsClient.PaymentAsync(new RecordPaymentCommand
            {
                MaintenanceBillId = BillId,
                Amount = (double)Amount,
                PaymentDate = PaymentDate,
                PaymentMode = SelectedPaymentMode.Value,
                TransactionReference = string.IsNullOrWhiteSpace(TransactionReference) ? null : TransactionReference,
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes
            });

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlert("Record Payment", "Payment recorded.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't record the payment ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
