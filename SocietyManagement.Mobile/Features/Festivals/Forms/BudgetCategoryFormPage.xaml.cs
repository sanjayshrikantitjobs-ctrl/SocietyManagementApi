using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class BudgetCategoryFormPage : ContentPage, IQueryAttributable
{
    private readonly BudgetCategoryFormViewModel _viewModel;

    public BudgetCategoryFormPage(BudgetCategoryFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("category", out var category) && category is FestivalBudgetCategoryDto dto) _viewModel.LoadFrom(dto);
    }
}
