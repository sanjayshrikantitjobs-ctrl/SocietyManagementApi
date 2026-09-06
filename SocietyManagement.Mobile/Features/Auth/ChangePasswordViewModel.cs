using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Auth;

/// <summary>Mirrors change-password.component.ts's self-service form —
/// reached from the top bar's user menu (see TopBarView.xaml.cs), same as
/// the web app's own avatar dropdown.</summary>
public partial class ChangePasswordViewModel : ObservableObject
{
    private readonly AuthClient _authClient;

    public ChangePasswordViewModel(AuthClient authClient)
    {
        _authClient = authClient;
    }

    [ObservableProperty] private string currentPassword = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? successMessage;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Enter your current and new password.";
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "New password and confirmation don't match.";
            return;
        }

        IsBusy = true;
        try
        {
            await _authClient.ChangePasswordAsync(new ChangePasswordCommand
            {
                CurrentPassword = CurrentPassword,
                NewPassword = NewPassword
            });

            SuccessMessage = "Password changed successfully.";
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't change your password ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
