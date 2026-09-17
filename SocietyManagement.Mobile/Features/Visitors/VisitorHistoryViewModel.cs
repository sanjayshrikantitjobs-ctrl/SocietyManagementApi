using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Features.Visitors;

/// <summary>Binds directly to a VisitorVisitDto row (see VisitorHistoryPage.
/// xaml) rather than living in Shared/Converters, since it's specific to
/// this DTO's CheckIn/CheckOut fields.</summary>
public class VisitDurationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is VisitorVisitDto v ? VisitorHistoryViewModel.Duration(v) : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

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
    private readonly VisitorPurposesClient _purposesClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public VisitorHistoryViewModel(
        VisitorVisitsClient visitsClient, VisitorPurposesClient purposesClient,
        CurrentSocietyService currentSocietyService, AuthState authState)
    {
        _visitsClient = visitsClient;
        _purposesClient = purposesClient;
        _currentSocietyService = currentSocietyService;
        Auth = authState;
    }

    public AuthState Auth { get; }

    [ObservableProperty] private ObservableCollection<VisitorVisitDto> visits = new();
    [ObservableProperty] private ObservableCollection<VisitorPurposeDto> purposes = new();
    [ObservableProperty] private VisitorPurposeDto? selectedPurpose;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string search = string.Empty;
    [ObservableProperty] private DateTime fromDate = DateTime.Today;
    [ObservableProperty] private DateTime toDate = DateTime.Today;
    [ObservableProperty] private bool hasFromDate;
    [ObservableProperty] private bool hasToDate;

    partial void OnSelectedPurposeChanged(VisitorPurposeDto? value) => _ = LoadCommand.ExecuteAsync(null);

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
            var purposeId = SelectedPurpose?.Id;
            int? societyId = null;

            if (Auth.IsWatchman || Auth.IsAdmin)
            {
                societyId = await _currentSocietyService.GetSocietyIdAsync();
                if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

                var response = await _visitsClient.VisitorVisitsGETAsync(
                    societyId, null, null, null, from, to, search, purposeId, null, true, 1, 50);
                Visits = new ObservableCollection<VisitorVisitDto>(response.Data?.Items ?? new());
            }
            else
            {
                var response = await _visitsClient.Mine7Async(from, to, search, purposeId, null, true, 1, 50);
                Visits = new ObservableCollection<VisitorVisitDto>(response.Data?.Items ?? new());
            }

            if (Purposes.Count == 0)
            {
                societyId ??= await _currentSocietyService.GetSocietyIdAsync();
                if (societyId is not null)
                {
                    var purposesResponse = await _purposesClient.VisitorPurposesGETAsync(societyId, true);
                    Purposes = new ObservableCollection<VisitorPurposeDto>(purposesResponse.Data ?? new());
                }
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

    /// <summary>CheckIn→CheckOut is the visitor's actual time inside,
    /// distinct from RequestedAt — blank until check-out is logged.</summary>
    public static string Duration(VisitorVisitDto v)
    {
        if (v.CheckInTime is null || v.CheckOutTime is null) return "—";
        var minutes = (int)(v.CheckOutTime.Value - v.CheckInTime.Value).TotalMinutes;
        return minutes < 60 ? $"{minutes}m" : $"{minutes / 60}h {minutes % 60}m";
    }

    /// <summary>Re-sends the same visitor/flat/purpose/gate as a brand new
    /// request rather than mutating the old one — a repeat guest gets a
    /// fresh approval cycle, same as if the resident had filled the form
    /// again (mirrors visitors-landing.component.ts's reinvite()).</summary>
    [RelayCommand]
    private async Task ReinviteAsync(VisitorVisitDto visit)
    {
        try
        {
            await _visitsClient.VisitorVisitsPOSTAsync(new CreateVisitCommand
            {
                VisitorId = visit.VisitorId, FlatId = visit.FlatId, PurposeId = visit.PurposeId,
                GateId = visit.GateId, NumberOfVisitors = visit.NumberOfVisitors
            });
            if (Shell.Current is not null) await Shell.Current.DisplayAlert("Re-invited", $"{visit.VisitorName} re-invited.", "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't re-invite this visitor ({ex.Message}).";
        }
    }
}
