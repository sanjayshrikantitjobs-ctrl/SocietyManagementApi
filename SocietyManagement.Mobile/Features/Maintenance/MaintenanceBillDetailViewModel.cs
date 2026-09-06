using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Features.Maintenance.Payments;

namespace SocietyManagement.Mobile.Features.Maintenance;

/// <summary>Mirrors maintenance-bill-detail.component.ts: summary card,
/// bill items, payment history, plus the same Download PDF / Record
/// Payment / Mark as Unpaid / Resend WhatsApp actions as the Bills list's
/// row menu.</summary>
public partial class MaintenanceBillDetailViewModel : ObservableObject
{
    private readonly MaintenanceBillsClient _billsClient;

    public MaintenanceBillDetailViewModel(MaintenanceBillsClient billsClient)
    {
        _billsClient = billsClient;
    }

    [ObservableProperty] private int billId;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private MaintenanceBillDetailDto? bill;

    public bool CanRecordPayment => Bill is { Status: not Api.Generated.BillStatus.Paid, IsRolledForward: not true };
    public bool CanMarkUnpaid => Bill is { Status: not Api.Generated.BillStatus.Pending };

    /// <summary>Prefers the flat's current primary resident (live from
    /// FlatResidencies/Member, same source the Residents module uses) over
    /// OwnerNameSnapshot, which is frozen at bill-generation time and can
    /// drift from reality (a corrected name, a change of owner/tenant).
    /// Falls back to the snapshot only when no active resident is on file.</summary>
    public string DisplayOwnerName => Bill?.OwnerName ?? Bill?.TenantName ?? Bill?.OwnerNameSnapshot ?? "—";

    async partial void OnBillIdChanged(int value) => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (BillId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _billsClient.Bills2Async(BillId);
            Bill = response.Data;
            OnPropertyChanged(nameof(CanRecordPayment));
            OnPropertyChanged(nameof(CanMarkUnpaid));
            OnPropertyChanged(nameof(DisplayOwnerName));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the bill ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadPdfAsync()
    {
        if (Bill is null) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var file = await _billsClient.Pdf3Async(BillId);
            var path = Path.Combine(FileSystem.CacheDirectory, $"{Bill.InvoiceNumber}.pdf");
            using (file)
            {
                await using var output = File.Create(path);
                await file.Stream.CopyToAsync(output);
            }
            await Share.Default.RequestAsync(new ShareFileRequest { Title = Bill.InvoiceNumber, File = new ShareFile(path) });
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

    [RelayCommand]
    private async Task RecordPaymentAsync()
    {
        if (Bill is null || Shell.Current is null) return;

        await Shell.Current.GoToAsync(
            $"{nameof(RecordPaymentPage)}?billId={BillId}&invoiceNumber={Uri.EscapeDataString(Bill.InvoiceNumber ?? string.Empty)}&balance={Bill.Balance ?? 0}");
    }

    [RelayCommand]
    private async Task MarkUnpaidAsync()
    {
        if (Bill is null || Shell.Current is null) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "Mark as Unpaid", $"Reverse payment on {Bill.InvoiceNumber}? Any payments recorded against this bill will be voided.",
            "Mark as Unpaid", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _billsClient.MarkUnpaidAsync(BillId);
            await LoadAsync();
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

    [RelayCommand]
    private async Task ResendWhatsAppAsync()
    {
        if (Bill is null) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _billsClient.ResendWhatsapp2Async(BillId);
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
}
