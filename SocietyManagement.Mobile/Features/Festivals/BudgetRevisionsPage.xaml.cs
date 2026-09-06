namespace SocietyManagement.Mobile.Features.Festivals;

public partial class BudgetRevisionsPage : ContentPage, IQueryAttributable
{
    private readonly BudgetRevisionsViewModel _viewModel;

    public BudgetRevisionsPage(BudgetRevisionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoryName", out var name)) Title = name?.ToString();
        if (query.TryGetValue("categoryId", out var id)) _viewModel.CategoryId = (int)id;
    }
}
