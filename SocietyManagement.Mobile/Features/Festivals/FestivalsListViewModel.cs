using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Festivals;

/// <summary>Mirrors festivals-list.component.ts — every festival for the
/// current year. The 8-controller detail surface (budget, contributions,
/// sponsors, vendors, expenses, tasks, volunteers, pool/child linking) is
/// its own larger follow-up; this is the list entry point into that module.</summary>
public partial class FestivalsListViewModel : ObservableObject
{
    private readonly FestivalsClient _festivalsClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public FestivalsListViewModel(FestivalsClient festivalsClient, CurrentSocietyService currentSocietyService)
    {
        _festivalsClient = festivalsClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private ObservableCollection<FestivalDto> festivals = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    /// <summary>Standalone (1) + Contribution Pool (2) — mirrors the web's
    /// "Primary Festivals & Events" section.</summary>
    [ObservableProperty] private ObservableCollection<FestivalDto> primaryFestivals = new();

    /// <summary>Child (3) — draws funding from a pool rather than standing
    /// alone, so it gets its own section, mirroring the web.</summary>
    [ObservableProperty] private ObservableCollection<FestivalDto> childFestivals = new();

    public bool HasChildFestivals => ChildFestivals.Count > 0;

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
            var response = await _festivalsClient.FestivalsGETAsync(societyId, null, null, 1, 50);
            Festivals = new ObservableCollection<FestivalDto>(response.Data?.Items ?? new());
            PrimaryFestivals = new ObservableCollection<FestivalDto>(Festivals.Where(f => f.Kind != FestivalKind.Child));
            ChildFestivals = new ObservableCollection<FestivalDto>(Festivals.Where(f => f.Kind == FestivalKind.Child));
            OnPropertyChanged(nameof(HasChildFestivals));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load festivals ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
