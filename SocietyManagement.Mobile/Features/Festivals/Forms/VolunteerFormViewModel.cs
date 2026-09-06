using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public partial class VolunteerFormViewModel : ObservableObject
{
    private readonly FestivalVolunteersClient _client;

    public VolunteerFormViewModel(FestivalVolunteersClient client)
    {
        _client = client;
    }

    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string? phone;
    [ObservableProperty] private string? email;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    public void LoadFrom(FestivalVolunteerDto volunteer)
    {
        Id = volunteer.Id ?? 0;
        Name = volunteer.Name ?? string.Empty;
        Phone = volunteer.Phone;
        Email = volunteer.Email;
        Notes = volunteer.Notes;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
                await _client.FestivalVolunteersPUTAsync(Id, new UpdateVolunteerCommand { Id = Id, Name = Name, Phone = Phone, Email = Email, Notes = Notes });
            else
                await _client.FestivalVolunteersPOSTAsync(new CreateVolunteerCommand { FestivalId = FestivalId, Name = Name, Phone = Phone, Email = Email, Notes = Notes });

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the volunteer ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
