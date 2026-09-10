namespace SocietyManagement.Mobile.Features.Finance.Forms;

public partial class FinanceExpenseFormPage : ContentPage, IQueryAttributable
{
    private readonly FinanceExpenseFormViewModel _viewModel;

    public FinanceExpenseFormPage(FinanceExpenseFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("expenseId", out var expenseId)) _viewModel.Id = (int)expenseId;
    }
}
