using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Visitors.Forms;

public partial class PurposeFormViewModel : ObservableObject
{
    private readonly VisitorPurposesClient _client;

    public PurposeFormViewModel(VisitorPurposesClient client)
    {
        _client = client;
    }

    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private bool requiresApproval = true;
    [ObservableProperty] private int displayOrder;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    public void LoadFrom(VisitorPurposeDto purpose)
    {
        Id = purpose.Id ?? 0;
        Name = purpose.Name ?? string.Empty;
        RequiresApproval = purpose.RequiresApproval ?? true;
        DisplayOrder = purpose.DisplayOrder ?? 0;
        IsActive = purpose.IsActive ?? true;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Enter a name.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.VisitorPurposesPUTAsync(Id, new UpdatePurposeCommand
                {
                    Id = Id, Name = Name, RequiresApproval = RequiresApproval, IsActive = IsActive, DisplayOrder = DisplayOrder
                });
            }
            else
            {
                await _client.VisitorPurposesPOSTAsync(new CreatePurposeCommand
                {
                    SocietyId = SocietyId, Name = Name, RequiresApproval = RequiresApproval, DisplayOrder = DisplayOrder
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the purpose ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
