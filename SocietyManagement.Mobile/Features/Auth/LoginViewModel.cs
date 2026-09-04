using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Dashboard;

namespace SocietyManagement.Mobile.Features.Auth;

/// <summary>Mirrors login.component.ts: identifier (email or mobile) +
/// password + optional society code — SuperAdmin skips the code, everyone
/// else needs it, but that's enforced server-side (LoginCommand), not here.</summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string identifier = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string societyCode = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Identifier) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your email or mobile number and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.LoginAsync(Identifier.Trim(), Password, SocietyCode.Trim());
            if (result.Success)
            {
                Password = string.Empty;
                await Shell.Current.GoToAsync($"//{nameof(DashboardPage)}");
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
