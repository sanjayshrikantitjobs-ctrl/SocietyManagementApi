namespace SocietyManagement.Mobile.Features.Auth;

/// <summary>Shell's initial route — a blank branded holding page shown only
/// for the moment it takes AppShell's session-restore check to resolve, so
/// a still-valid session never flashes the Login page before landing on
/// Dashboard (see AppShell.xaml.cs's OnLoaded).</summary>
public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
    }
}
