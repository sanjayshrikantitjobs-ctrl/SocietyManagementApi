namespace SocietyManagement.Mobile.Features.Festivals;

[QueryProperty(nameof(FestivalId), "festivalId")]
public partial class FestivalDetailPage : ContentPage
{
    private readonly FestivalDetailViewModel _viewModel;

    public FestivalDetailPage(FestivalDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string FestivalId
    {
        set
        {
            if (int.TryParse(value, out var id))
            {
                _viewModel.FestivalId = id;
            }
        }
    }
}
