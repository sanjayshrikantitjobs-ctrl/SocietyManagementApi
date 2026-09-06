namespace SocietyManagement.Mobile.Features.Maintenance;

[QueryProperty(nameof(BillId), "billId")]
public partial class MaintenanceBillDetailPage : ContentPage
{
    private readonly MaintenanceBillDetailViewModel _viewModel;

    public MaintenanceBillDetailPage(MaintenanceBillDetailViewModel viewModel)
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
}
