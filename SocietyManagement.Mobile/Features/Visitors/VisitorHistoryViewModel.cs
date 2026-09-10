using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Features.Visitors;

/// <summary>Mirrors visitors-landing.component.ts's "Recent Visitors" table —
/// server-paginated visitor history with a search box and From/To date
/// filters, admin/watchman seeing every visit in the society
/// (VisitorVisitsGETAsync) and a resident seeing only their own flat's
/// (Mine5Async). Tapping a row opens VisitorVisitDetailPage with the
/// already-loaded row passed straight through — no GetById endpoint is
/// needed since the list query already returns every field the detail
/// view shows, same pattern used for EmergencyContactDto/VehicleDto
/// elsewhere in this app.</summary>
public partial class VisitorHistoryViewModel : ObservableObject
{
    private readonly VisitorVisitsClient _visitsClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public VisitorHistoryViewModel(VisitorVisitsClient visitsClient, CurrentSocietyService currentSocietyService, AuthState authState)
    {
        _visitsClient = visitsClient;
        _currentSocietyService = currentSocietyService;
        Auth = authState;
    }

    public AuthState Auth { get; }

    [ObservableProperty] private ObservableCollection<VisitorVisitDto> visits = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string search = string.Empty;
    [ObservableProperty] private DateTime fromDate = DateTime.Today;
    [ObservableProperty] private DateTime toDate = DateTime.Today;
    [ObservableProperty] private bool hasFromDate;
    [ObservableProperty] private bool hasToDate;

    partial void OnFromDateChanged(DateTime value)
    {
        HasFromDate = true;
        _ = LoadCommand.ExecuteAsync(null);
    }

    partial void OnToDateChanged(DateTime value)
    {
        HasToDate = true;
        _ = LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearFromDate()
    {
        HasFromDate = false;
        _ = LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearToDate()
    {
        HasToDate = false;
        _ = LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var from = HasFromDate ? FromDate : (DateTime?)null;
            var to = HasToDate ? ToDate : (DateTime?)null;
            var search = string.IsNullOrWhiteSpace(Search) ? null : Search;

            if (Auth.IsWatchman || Auth.IsAdmin)
            {
                var societyId = await _currentSocietyService.GetSocietyIdAsync();
                if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

                var response = await _visitsClient.VisitorVisitsGETAsync(
                    societyId, null, null, null, from, to, search, null, true, 1, 50);
                Visits = new ObservableCollection<VisitorVisitDto>(response.Data?.Items ?? new());
            }
            else
            {
                var response = await _visitsClient.Mine5Async(from, to, search, null, true, 1, 50);
                Visits = new ObservableCollection<VisitorVisitDto>(response.Data?.Items ?? new());
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load visitor history ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDetailAsync(VisitorVisitDto visit)
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(VisitorVisitDetailPage), new Dictionary<string, object> { ["visit"] = visit });
    }
}
