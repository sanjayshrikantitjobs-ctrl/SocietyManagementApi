using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Announcements.Forms;

public partial class AnnouncementFormPage : ContentPage, IQueryAttributable
{
    private readonly AnnouncementFormViewModel _viewModel;

    public AnnouncementFormPage(AnnouncementFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("societyId", out var societyId)) _viewModel.SocietyId = (int)societyId;
        if (query.TryGetValue("announcement", out var announcement) && announcement is AnnouncementDto dto) _viewModel.LoadFrom(dto);
    }
}
