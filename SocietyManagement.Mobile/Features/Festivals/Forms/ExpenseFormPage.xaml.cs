using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class ExpenseFormPage : ContentPage, IQueryAttributable
{
    private readonly ExpenseFormViewModel _viewModel;

    public ExpenseFormPage(ExpenseFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("expense", out var expense) && expense is FestivalExpenseDto dto) _viewModel.LoadFrom(dto);
    }
}
