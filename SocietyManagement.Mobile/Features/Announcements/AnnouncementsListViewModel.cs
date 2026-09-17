using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Announcements.Forms;

namespace SocietyManagement.Mobile.Features.Announcements;

/// <summary>Mirrors announcements-list.component.ts: Admin/SuperAdmin see
/// every status via AnnouncementsGETAsync; everyone else sees only the
/// published, not-yet-expired feed via PublishedAsync, with per-user IsRead
/// resolved server-side. Manage actions are gated by Auth.IsAdmin — every
/// existing Mobile feature gates by role rather than by the granular
/// "notices.manage" permission code (see GatesViewModel), and Notices.Manage
/// is only ever granted to Admin/SuperAdmin in this app, so the two checks
/// are equivalent in practice.</summary>
public partial class AnnouncementsListViewModel : ObservableObject
{
    private readonly AnnouncementsClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public AnnouncementsListViewModel(AnnouncementsClient client, CurrentSocietyService currentSocietyService, AuthState authState)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
        Auth = authState;
    }

    public AuthState Auth { get; }

    private List<AnnouncementDto> _allAnnouncements = new();

    [ObservableProperty] private ObservableCollection<AnnouncementDto> announcements = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    /// <summary>"All" | "Unread" | "Saved" — mirrors announcements-list.
    /// component.ts's readFilter; only meaningful for a resident's own feed,
    /// since the admin list never resolves IsRead/IsSaved per-user.</summary>
    [ObservableProperty] private string readFilter = "All";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = Auth.IsAdmin
                ? await _client.AnnouncementsGETAsync(societyId, null, null, null, 1, 100)
                : await _client.PublishedAsync(societyId, 1, 100);
            _allAnnouncements = response.Data?.Items?.ToList() ?? new();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load announcements ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        ReadFilter = filter;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<AnnouncementDto> filtered = ReadFilter switch
        {
            "Unread" => _allAnnouncements.Where(a => a.IsRead != true),
            "Saved" => _allAnnouncements.Where(a => a.IsSaved == true),
            _ => _allAnnouncements
        };
        Announcements = new ObservableCollection<AnnouncementDto>(filtered);
    }

    [RelayCommand]
    private async Task ToggleSavedAsync(AnnouncementDto announcement)
    {
        if (announcement.Id is not int id) return;
        try
        {
            var isSaved = (await _client.ToggleSavedAsync(id)).Data;
            announcement.IsSaved = isSaved;
            // Re-run the current filter so toggling off "Saved" while that
            // filter is active removes the row immediately, matching the
            // web's computed-signal behavior.
            var index = _allAnnouncements.FindIndex(a => a.Id == id);
            if (index >= 0) _allAnnouncements[index] = announcement;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update saved status ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task AddAnnouncementAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;
        await Shell.Current.GoToAsync(nameof(AnnouncementFormPage), new Dictionary<string, object> { ["societyId"] = societyId });
    }

    [RelayCommand]
    private async Task ShowActionsAsync(AnnouncementDto announcement)
    {
        if (Shell.Current is null || announcement.Id is not int id) return;

        var options = announcement.Status is AnnouncementStatus.Draft or AnnouncementStatus.Scheduled
            ? new[] { "Edit", "Publish Now", "Delete" }
            : new[] { "Edit", "Delete" };

        var choice = await Shell.Current.DisplayActionSheet(announcement.Title, "Cancel", null, options);
        switch (choice)
        {
            case "Edit":
                await Shell.Current.GoToAsync(nameof(AnnouncementFormPage), new Dictionary<string, object> { ["announcement"] = announcement });
                break;
            case "Publish Now":
                try
                {
                    await _client.PublishAsync(id);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't publish the announcement ({ex.Message}).";
                }
                break;
            case "Delete":
                var confirmed = await Shell.Current.DisplayAlert("Delete Announcement", $"Delete \"{announcement.Title}\"?", "Delete", "Cancel");
                if (!confirmed) return;
                try
                {
                    await _client.AnnouncementsDELETEAsync(id);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Couldn't delete the announcement ({ex.Message}).";
                }
                break;
        }
    }
}
