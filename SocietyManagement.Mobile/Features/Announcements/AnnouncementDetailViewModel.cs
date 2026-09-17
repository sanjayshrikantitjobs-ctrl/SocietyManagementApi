using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Features.Announcements;

/// <summary>Mirrors announcement-detail.component.ts: loads the single
/// announcement, and for a non-admin viewer who hasn't read it yet, fires
/// MarkReadAsync once the load completes (same "mark read on open" trigger
/// the web page uses in its ngOnInit).</summary>
public partial class AnnouncementDetailViewModel : ObservableObject
{
    private readonly AnnouncementsClient _client;

    public AnnouncementDetailViewModel(AnnouncementsClient client, AuthState authState)
    {
        _client = client;
        Auth = authState;
    }

    public AuthState Auth { get; }

    [ObservableProperty] private int announcementId;
    [ObservableProperty] private AnnouncementDto? announcement;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    async partial void OnAnnouncementIdChanged(int value) => await LoadAsync();

    private async Task LoadAsync()
    {
        if (AnnouncementId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.AnnouncementsGET2Async(AnnouncementId);
            Announcement = response.Data;

            if (!Auth.IsAdmin && Announcement?.IsRead == false)
            {
                await _client.MarkReadAsync(AnnouncementId);
                Announcement.IsRead = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the announcement ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PublishAsync()
    {
        if (Announcement?.Id is not int id) return;
        try
        {
            await _client.PublishAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't publish the announcement ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Announcement?.Id is not int id || Shell.Current is null) return;

        var confirmed = await Shell.Current.DisplayAlert("Delete Announcement", $"Delete \"{Announcement.Title}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        try
        {
            await _client.AnnouncementsDELETEAsync(id);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete the announcement ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (Announcement is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(Forms.AnnouncementFormPage), new Dictionary<string, object> { ["announcement"] = Announcement });
    }
}
