using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Complaints;

public record ComplaintCategoryOption(string Label, ComplaintCategory? Value);
public record ComplaintPriorityOption(string Label, ComplaintPriority? Value);

/// <summary>Read-only pass mirroring the web's Complaints list (the web
/// also offers a Kanban "Board" view — not a natural touch/mobile
/// interaction, so this shows the same data as a single filterable list
/// instead, with the same status counts as a KPI strip). Assign/Start/
/// Resolve/Close/Reopen are a follow-up.</summary>
public partial class ComplaintsViewModel : ObservableObject
{
    private static readonly ComplaintCategoryOption[] CategoryOptionsSeed =
    {
        new("All Categories", null),
        new("Plumbing", Api.Generated.ComplaintCategory.Plumbing),
        new("Electrical", Api.Generated.ComplaintCategory.Electrical),
        new("Housekeeping", Api.Generated.ComplaintCategory.Housekeeping),
        new("Security", Api.Generated.ComplaintCategory.Security),
        new("Parking", Api.Generated.ComplaintCategory.Parking),
        new("Structural", Api.Generated.ComplaintCategory.Structural),
        new("Noise", Api.Generated.ComplaintCategory.Noise),
        new("Lift/Elevator", Api.Generated.ComplaintCategory.LiftElevator),
        new("Water Supply", Api.Generated.ComplaintCategory.WaterSupply),
    };

    private static readonly ComplaintPriorityOption[] PriorityOptionsSeed =
    {
        new("All Priorities", null),
        new("Low", Api.Generated.ComplaintPriority.Low),
        new("Medium", Api.Generated.ComplaintPriority.Medium),
        new("High", Api.Generated.ComplaintPriority.High),
    };

    private readonly ComplaintsClient _client;
    private readonly CurrentSocietyService _currentSocietyService;

    public ComplaintsViewModel(ComplaintsClient client, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _currentSocietyService = currentSocietyService;
    }

    public ObservableCollection<ComplaintCategoryOption> CategoryOptions { get; } = new(CategoryOptionsSeed);
    public ObservableCollection<ComplaintPriorityOption> PriorityOptions { get; } = new(PriorityOptionsSeed);

    [ObservableProperty] private ComplaintKpisDto? kpis;
    [ObservableProperty] private ObservableCollection<ComplaintDto> complaints = new();
    [ObservableProperty] private string search = string.Empty;
    [ObservableProperty] private ComplaintCategoryOption selectedCategory = CategoryOptionsSeed[0];
    [ObservableProperty] private ComplaintPriorityOption selectedPriority = PriorityOptionsSeed[0];
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    partial void OnSelectedCategoryChanged(ComplaintCategoryOption value) => _ = LoadCommand.ExecuteAsync(null);
    partial void OnSelectedPriorityChanged(ComplaintPriorityOption value) => _ = LoadCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var kpisResponse = await _client.KpisAsync(societyId);
            Kpis = kpisResponse.Data;

            var response = await _client.PagedAsync(
                societyId, SelectedCategory.Value, SelectedPriority.Value,
                string.IsNullOrWhiteSpace(Search) ? null : Search, null, false, 1, 100);
            Complaints = new ObservableCollection<ComplaintDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load complaints ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
