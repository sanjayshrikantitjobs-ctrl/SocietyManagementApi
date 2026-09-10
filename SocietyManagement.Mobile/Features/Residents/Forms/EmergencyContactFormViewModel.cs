using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Residents.Forms;

public partial class EmergencyContactFormViewModel : ObservableObject
{
    private readonly EmergencyContactsClient _client;

    public EmergencyContactFormViewModel(EmergencyContactsClient client) => _client = client;

    [ObservableProperty] private int id;
    [ObservableProperty] private int flatId;
    [ObservableProperty] private string contactName = string.Empty;
    [ObservableProperty] private string relationship = string.Empty;
    [ObservableProperty] private string phone = string.Empty;
    [ObservableProperty] private string? alternatePhone;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    /// <summary>No GetById endpoint exists for emergency contacts (only a
    /// per-flat list) — the row tapped in the flat detail page is passed
    /// straight through instead of a redundant re-fetch by id.</summary>
    public void LoadFrom(EmergencyContactDto contact)
    {
        Id = contact.Id ?? 0;
        FlatId = contact.FlatId ?? 0;
        ContactName = contact.ContactName ?? string.Empty;
        Relationship = contact.Relationship ?? string.Empty;
        Phone = contact.Phone ?? string.Empty;
        AlternatePhone = contact.AlternatePhone;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(ContactName) || string.IsNullOrWhiteSpace(Relationship) || string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = "Name, relationship and phone are required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.EmergencyContactsPUTAsync(Id, new UpdateEmergencyContactCommand
                {
                    Id = Id, ContactName = ContactName, Relationship = Relationship, Phone = Phone, AlternatePhone = AlternatePhone
                });
            }
            else
            {
                await _client.EmergencyContactsPOSTAsync(new CreateEmergencyContactCommand
                {
                    FlatId = FlatId, ContactName = ContactName, Relationship = Relationship, Phone = Phone, AlternatePhone = AlternatePhone
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save this contact ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
