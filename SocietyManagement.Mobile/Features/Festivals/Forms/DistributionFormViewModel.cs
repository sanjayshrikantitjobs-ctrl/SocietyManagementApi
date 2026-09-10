using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public record DistributionEligibilityOption(string Label, DistributionEligibilityType Value);

/// <summary>Mirrors festival-distributions-tab.component.ts's Add dialog and
/// festival-distribution-detail-dialog.component.ts's Edit dialog — the two
/// are near-identical except Edit drops the create-only VariantLabels field
/// (variants are managed separately once the distribution exists).</summary>
public partial class DistributionFormViewModel : ObservableObject
{
    private readonly FestivalDistributionsClient _client;

    private static readonly DistributionEligibilityOption[] EligibilityOptionsSeed =
    {
        new("Every Flat", DistributionEligibilityType.AllFlats),
        new("Partially or Fully Paid Contribution", DistributionEligibilityType.ContributionPaid),
        new("Minimum Contribution Amount", DistributionEligibilityType.MinimumAmount),
    };

    public DistributionFormViewModel(FestivalDistributionsClient client)
    {
        _client = client;
        selectedEligibility = EligibilityOptionsSeed[0];
    }

    public DistributionEligibilityOption[] EligibilityOptions => EligibilityOptionsSeed;

    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string itemName = string.Empty;
    [ObservableProperty] private string? description;
    [ObservableProperty] private int quantityPerFlat = 1;
    [ObservableProperty] private DistributionEligibilityOption selectedEligibility;
    [ObservableProperty] private decimal eligibilityMinContribution;
    [ObservableProperty] private string variantLabels = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;
    public bool ShowVariantLabels => !IsEditMode;
    public bool ShowMinContribution => SelectedEligibility.Value == DistributionEligibilityType.MinimumAmount;

    partial void OnSelectedEligibilityChanged(DistributionEligibilityOption value) => OnPropertyChanged(nameof(ShowMinContribution));

    public void LoadFrom(FestivalDistributionDto distribution)
    {
        Id = distribution.Id ?? 0;
        ItemName = distribution.ItemName ?? string.Empty;
        Description = distribution.Description;
        QuantityPerFlat = distribution.QuantityPerFlat ?? 1;
        SelectedEligibility = EligibilityOptionsSeed.FirstOrDefault(o => o.Value == distribution.EligibilityType) ?? EligibilityOptionsSeed[0];
        EligibilityMinContribution = (decimal)(distribution.EligibilityMinContribution ?? 0);
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(ShowVariantLabels));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            ErrorMessage = "Enter an item name.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var minContribution = SelectedEligibility.Value == DistributionEligibilityType.MinimumAmount
                ? (double?)EligibilityMinContribution : null;

            if (IsEditMode)
            {
                await _client.FestivalDistributionsPUTAsync(Id, new UpdateDistributionCommand
                {
                    Id = Id, ItemName = ItemName, Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                    EligibilityType = SelectedEligibility.Value, EligibilityMinContribution = minContribution,
                    QuantityPerFlat = QuantityPerFlat
                });
            }
            else
            {
                var variantLabels = VariantLabels.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                await _client.FestivalDistributionsPOSTAsync(new CreateDistributionCommand
                {
                    FestivalId = FestivalId, ItemName = ItemName, Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                    EligibilityType = SelectedEligibility.Value, EligibilityMinContribution = minContribution,
                    QuantityPerFlat = QuantityPerFlat, VariantLabels = new(variantLabels)
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the distribution ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
