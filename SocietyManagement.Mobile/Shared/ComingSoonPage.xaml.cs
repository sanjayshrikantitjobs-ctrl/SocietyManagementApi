namespace SocietyManagement.Mobile.Shared;

[QueryProperty(nameof(PageTitle), "title")]
public partial class ComingSoonPage : ContentPage
{
    private string _pageTitle = string.Empty;

    public string PageTitle
    {
        get => _pageTitle;
        set
        {
            _pageTitle = Uri.UnescapeDataString(value ?? string.Empty);
            Title = _pageTitle;
            OnPropertyChanged();
        }
    }

    public ComingSoonPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Reached two ways: a query-string navigation (Home page tiles —
        // PageTitle already set via the "title" query property), or tapping
        // a flyout entry directly (AppShell.xaml's placeholder FlyoutItems —
        // no query string, so fall back to that flyout item's own Title).
        if (string.IsNullOrEmpty(_pageTitle) && Shell.Current?.CurrentItem?.Title is string flyoutTitle)
        {
            PageTitle = flyoutTitle;
        }
    }
}
