using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class TaskFormPage : ContentPage, IQueryAttributable
{
    private readonly TaskFormViewModel _viewModel;

    public TaskFormPage(TaskFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("festivalId", out var festivalId)) _viewModel.FestivalId = (int)festivalId;
        if (query.TryGetValue("task", out var task) && task is FestivalTaskDto dto) _viewModel.LoadFrom(dto);
    }
}
