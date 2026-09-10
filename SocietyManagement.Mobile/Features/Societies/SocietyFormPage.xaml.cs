namespace SocietyManagement.Mobile.Features.Societies;

public partial class SocietyFormPage : ContentPage, IQueryAttributable
{
    private readonly SocietyFormViewModel _viewModel;

    public SocietyFormPage(SocietyFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.Id = (int)societyId;
    }
}
