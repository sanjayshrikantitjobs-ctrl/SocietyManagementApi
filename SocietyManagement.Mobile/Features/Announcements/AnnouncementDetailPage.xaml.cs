namespace SocietyManagement.Mobile.Features.Announcements;

[QueryProperty(nameof(AnnouncementIdParam), "announcementId")]
public partial class AnnouncementDetailPage : ContentPage
{
    private readonly AnnouncementDetailViewModel _viewModel;

    public AnnouncementDetailPage(AnnouncementDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string AnnouncementIdParam
    {
        set { if (int.TryParse(value, out var id)) _viewModel.AnnouncementId = id; }
    }
}
