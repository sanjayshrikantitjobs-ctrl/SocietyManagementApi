using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Visitors;

/// <summary>Mirrors currently-inside.component.ts — every visitor who has
/// checked in but not yet checked out, with a one-tap check-out.</summary>
public partial class CurrentlyInsideViewModel : ObservableObject
{
    private readonly VisitorVisitsClient _visitsClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public CurrentlyInsideViewModel(VisitorVisitsClient visitsClient, CurrentSocietyService currentSocietyService)
    {
        _visitsClient = visitsClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private ObservableCollection<VisitorVisitDto> visits = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null)
        {
            ErrorMessage = "No society available for this account.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _visitsClient.CurrentlyInsideAsync(societyId);
            Visits = new ObservableCollection<VisitorVisitDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the list ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckOutAsync(VisitorVisitDto visit)
    {
        if (visit.Id is not int id) return;

        IsBusy = true;
        try
        {
            await _visitsClient.CheckOutAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't check out {visit.VisitorName} ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
