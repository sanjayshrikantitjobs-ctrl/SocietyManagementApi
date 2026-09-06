using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public record BudgetCategoryOption(string Label, FestivalBudgetCategoryType Value);

/// <summary>Mirrors festival-budget-tab.component.ts's Add/Edit Category
/// dialog — Category itself is fixed once created (UpdateBudgetCategoryCommand
/// has no Category field), so the picker is disabled in edit mode.</summary>
public partial class BudgetCategoryFormViewModel : ObservableObject
{
    private readonly FestivalBudgetCategoriesClient _client;

    public BudgetCategoryFormViewModel(FestivalBudgetCategoriesClient client)
    {
        _client = client;
        selectedCategory = CategoryOptions[0];
    }

    public List<BudgetCategoryOption> CategoryOptions { get; } = new()
    {
        new("Decoration", FestivalBudgetCategoryType.Decoration), new("Lighting", FestivalBudgetCategoryType.Lighting),
        new("Sound", FestivalBudgetCategoryType.Sound), new("Food", FestivalBudgetCategoryType.Food),
        new("Idol", FestivalBudgetCategoryType.Idol), new("Pandal", FestivalBudgetCategoryType.Pandal),
        new("Stage", FestivalBudgetCategoryType.Stage), new("Security", FestivalBudgetCategoryType.Security),
        new("Cleaning", FestivalBudgetCategoryType.Cleaning), new("Generator", FestivalBudgetCategoryType.Generator),
        new("Photography", FestivalBudgetCategoryType.Photography), new("Miscellaneous", FestivalBudgetCategoryType.Miscellaneous),
        new("Custom", FestivalBudgetCategoryType.Custom),
    };

    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int id;
    [ObservableProperty] private BudgetCategoryOption selectedCategory;
    [ObservableProperty] private string? customCategoryName;
    [ObservableProperty] private decimal estimatedAmount;
    [ObservableProperty] private decimal approvedAmount;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private string? reason;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;
    public bool ShowCustomName => SelectedCategory?.Value == FestivalBudgetCategoryType.Custom;

    partial void OnSelectedCategoryChanged(BudgetCategoryOption value) => OnPropertyChanged(nameof(ShowCustomName));

    public void LoadFrom(FestivalBudgetCategoryDto category)
    {
        Id = category.Id ?? 0;
        SelectedCategory = CategoryOptions.FirstOrDefault(o => o.Value == category.Category) ?? CategoryOptions[0];
        CustomCategoryName = category.CustomCategoryName;
        EstimatedAmount = (decimal)(category.EstimatedAmount ?? 0);
        ApprovedAmount = (decimal)(category.ApprovedAmount ?? 0);
        Notes = category.Notes;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.FestivalBudgetCategoriesPUTAsync(Id, new UpdateBudgetCategoryCommand
                {
                    Id = Id, EstimatedAmount = (double)EstimatedAmount, ApprovedAmount = (double)ApprovedAmount,
                    Notes = Notes, Reason = Reason
                });
            }
            else
            {
                await _client.FestivalBudgetCategoriesPOSTAsync(new CreateBudgetCategoryCommand
                {
                    FestivalId = FestivalId, Category = SelectedCategory.Value,
                    CustomCategoryName = SelectedCategory.Value == FestivalBudgetCategoryType.Custom ? CustomCategoryName : null,
                    EstimatedAmount = (double)EstimatedAmount, ApprovedAmount = (double)ApprovedAmount, Notes = Notes
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the category ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
