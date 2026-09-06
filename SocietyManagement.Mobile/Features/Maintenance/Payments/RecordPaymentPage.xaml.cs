namespace SocietyManagement.Mobile.Features.Maintenance.Payments;

[QueryProperty(nameof(BillId), "billId")]
[QueryProperty(nameof(InvoiceNumber), "invoiceNumber")]
[QueryProperty(nameof(Balance), "balance")]
public partial class RecordPaymentPage : ContentPage
{
    private readonly RecordPaymentViewModel _viewModel;

    public RecordPaymentPage(RecordPaymentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string BillId
    {
        set
        {
            if (int.TryParse(value, out var id))
            {
                _viewModel.BillId = id;
            }
        }
    }

    public string InvoiceNumber
    {
        set => _viewModel.InvoiceNumber = Uri.UnescapeDataString(value ?? string.Empty);
    }

    public string Balance
    {
        set => _viewModel.SetBalance(value);
    }
}
