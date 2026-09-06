using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Visitors.Forms;

public partial class GateFormViewModel : ObservableObject
{
    private readonly GatesClient _client;

    public GateFormViewModel(GatesClient client)
    {
        _client = client;
    }

    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string code = string.Empty;
    [ObservableProperty] private string? location;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    public void LoadFrom(GateDto gate)
    {
        Id = gate.Id ?? 0;
        Name = gate.Name ?? string.Empty;
        Code = gate.Code ?? string.Empty;
        Location = gate.Location;
        IsActive = gate.IsActive ?? true;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Code))
        {
            ErrorMessage = "Enter a name and code.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.GatesPUTAsync(Id, new UpdateGateCommand
                {
                    Id = Id, Name = Name, Code = Code, Location = string.IsNullOrWhiteSpace(Location) ? null : Location, IsActive = IsActive
                });
            }
            else
            {
                await _client.GatesPOSTAsync(new CreateGateCommand
                {
                    SocietyId = SocietyId, Name = Name, Code = Code, Location = string.IsNullOrWhiteSpace(Location) ? null : Location
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the gate ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
