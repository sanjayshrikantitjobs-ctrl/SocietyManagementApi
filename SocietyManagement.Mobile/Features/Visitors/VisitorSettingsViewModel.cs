using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Visitors;

/// <summary>Mirrors visitor-settings.component.ts's two-field form.</summary>
public partial class VisitorSettingsViewModel : ObservableObject
{
    private readonly VisitorSettingsClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public VisitorSettingsViewModel(VisitorSettingsClient client, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private int approvalRequestExpiryMinutes = 15;
    [ObservableProperty] private int retentionDays = 30;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.VisitorSettingsGETAsync(societyId);
            if (response.Data is { } settings)
            {
                ApprovalRequestExpiryMinutes = settings.ApprovalRequestExpiryMinutes ?? 15;
                RetentionDays = settings.RetentionDays ?? 30;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load settings ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _client.VisitorSettingsPUTAsync(new UpsertVisitorSettingsCommand
            {
                SocietyId = societyId, ApprovalRequestExpiryMinutes = ApprovalRequestExpiryMinutes, RetentionDays = RetentionDays
            });
            if (Shell.Current is not null) await Shell.Current.DisplayAlert("Settings", "Visitor settings updated.", "OK");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save settings ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
