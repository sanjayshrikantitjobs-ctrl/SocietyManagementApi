using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Residents.Forms;

public record RelationshipOption(string Label, PersonRelationship Value);

/// <summary>Add/Edit a resident on a flat. Three distinct "add" shapes share
/// this one form because the web itself does — adding the very first
/// Tenant on a flat opens a brand-new FlatOccupancy episode
/// (AddTenantOccupancyCommand), while every Owner add and every
/// non-first Tenant add just adds another member to the existing episode
/// (AddOwnerMemberCommand / AddTenantFamilyMemberCommand). Editing instead
/// updates the underlying Person record — the web's own "Edit Member"
/// dialog does the same (person fields, not occupancy fields).</summary>
public partial class OccupancyMemberFormViewModel : ObservableObject
{
    private readonly FlatOccupanciesClient _occupanciesClient;
    private readonly PersonsClient _personsClient;

    public OccupancyMemberFormViewModel(FlatOccupanciesClient occupanciesClient, PersonsClient personsClient)
    {
        _occupanciesClient = occupanciesClient;
        _personsClient = personsClient;
        selectedRelationship = RelationshipOptions[0];
    }

    public List<RelationshipOption> RelationshipOptions { get; } = new()
    {
        new("Self", PersonRelationship.Self), new("Spouse", PersonRelationship.Spouse), new("Son", PersonRelationship.Son),
        new("Daughter", PersonRelationship.Daughter), new("Parent", PersonRelationship.Parent),
        new("Grandparent", PersonRelationship.Grandparent), new("Sibling", PersonRelationship.Sibling), new("Other", PersonRelationship.Other),
    };

    /// <summary>"OwnerMember" | "TenantNew" | "TenantFamily" — which Add
    /// command to call; empty/unset in Edit mode (PersonId already set).</summary>
    [ObservableProperty] private string mode = string.Empty;
    [ObservableProperty] private int flatId;
    [ObservableProperty] private int flatOccupancyId;
    [ObservableProperty] private int personId;

    [ObservableProperty] private string firstName = string.Empty;
    [ObservableProperty] private string lastName = string.Empty;
    [ObservableProperty] private string phone = string.Empty;
    [ObservableProperty] private string? email;
    [ObservableProperty] private string? whatsAppNumber;
    [ObservableProperty] private RelationshipOption selectedRelationship;
    [ObservableProperty] private bool isPrimary;
    [ObservableProperty] private DateTime moveInDate = DateTime.Today;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => PersonId > 0;
    public bool ShowRelationshipFields => Mode != "TenantNew";

    async partial void OnPersonIdChanged(int value)
    {
        if (value > 0) await LoadPersonAsync();
    }

    private async Task LoadPersonAsync()
    {
        IsBusy = true;
        try
        {
            var response = await _personsClient.PersonsGETAsync(PersonId);
            var person = response.Data;
            if (person is null) return;

            FirstName = person.FirstName ?? string.Empty;
            LastName = person.LastName ?? string.Empty;
            Phone = person.Phone ?? string.Empty;
            Email = person.Email;
            WhatsAppNumber = person.WhatsAppNumber;
            OnPropertyChanged(nameof(IsEditMode));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load this person ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = "First name, last name and phone are required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _personsClient.PersonsPUTAsync(PersonId, new UpdatePersonCommand
                {
                    Id = PersonId, FirstName = FirstName, LastName = LastName, Phone = Phone,
                    Email = Email, WhatsAppNumber = WhatsAppNumber
                });
            }
            else
            {
                switch (Mode)
                {
                    case "OwnerMember":
                        await _occupanciesClient.OwnerMemberAsync(new AddOwnerMemberCommand
                        {
                            FlatId = FlatId, FirstName = FirstName, LastName = LastName, Phone = Phone, Email = Email,
                            WhatsAppNumber = WhatsAppNumber, Relationship = SelectedRelationship.Value, IsPrimary = IsPrimary,
                            MoveInDate = MoveInDate
                        });
                        break;
                    case "TenantNew":
                        await _occupanciesClient.TenantAsync(new AddTenantOccupancyCommand
                        {
                            FlatId = FlatId, FirstName = FirstName, LastName = LastName, Phone = Phone, Email = Email,
                            WhatsAppNumber = WhatsAppNumber, MoveInDate = MoveInDate
                        });
                        break;
                    case "TenantFamily":
                        await _occupanciesClient.FamilyMemberAsync(FlatOccupancyId, new AddFamilyMemberRequest
                        {
                            FirstName = FirstName, LastName = LastName, Phone = Phone, Email = Email,
                            WhatsAppNumber = WhatsAppNumber, Relationship = SelectedRelationship.Value, MoveInDate = MoveInDate
                        });
                        break;
                }
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save this member ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
