namespace SocietyManagement.Mobile.Features.Auth;

public partial class LoginPage : ContentPage
{
    private const string SupportPhoneNumber = "+91-9867343302";

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Opens the device dialer pre-filled with the Shrios support
    /// number — same intent as a "tel:" link on the web app's own contact
    /// footer, just the mobile-native way to reach it. Never places the
    /// call itself; the user still has to press Call.</summary>
    private async void OnContactUsTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            PhoneDialer.Default.Open(SupportPhoneNumber);
        }
        catch (Exception)
        {
            await DisplayAlert("Contact Us", $"Call or WhatsApp us at {SupportPhoneNumber}", "OK");
        }
    }
}
