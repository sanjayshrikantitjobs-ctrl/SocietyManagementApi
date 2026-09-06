using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals;

/// <summary>Read-only revision history — mirrors budget-revisions-dialog.component.ts,
/// which has no actions of its own, just a list of prior estimated/approved changes.</summary>
public partial class BudgetRevisionsViewModel : ObservableObject
{
    private readonly FestivalBudgetCategoriesClient _client;

    public BudgetRevisionsViewModel(FestivalBudgetCategoriesClient client)
    {
        _client = client;
    }

    [ObservableProperty] private int categoryId;
    [ObservableProperty] private string categoryName = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private ObservableCollection<FestivalBudgetRevisionDto> revisions = new();

    async partial void OnCategoryIdChanged(int value) => await LoadCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (CategoryId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.RevisionsAsync(CategoryId);
            Revisions = new ObservableCollection<FestivalBudgetRevisionDto>(response.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the history ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
