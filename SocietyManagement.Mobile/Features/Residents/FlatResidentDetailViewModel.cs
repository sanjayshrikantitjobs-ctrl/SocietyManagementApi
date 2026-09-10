using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Features.Residents.Forms;

namespace SocietyManagement.Mobile.Features.Residents;

/// <summary>One member's login-account status, for the Login Accounts card
/// — a per-row async lookup (GetLogin), same as the web's own
/// flat-login-card.component.ts.</summary>
public partial class MemberLoginItemViewModel : ObservableObject
{
    public MemberLoginItemViewModel(OccupancyMemberDto member)
    {
        PersonId = member.PersonId ?? 0;
        PersonName = member.PersonName ?? "—";
    }

    public int PersonId { get; }
    public string PersonName { get; }

    [ObservableProperty] private bool isChecking = true;
    [ObservableProperty] private PersonLoginDto? login;

    public bool ShowCreateLogin => !IsChecking && Login is null;

    partial void OnIsCheckingChanged(bool value) => OnPropertyChanged(nameof(ShowCreateLogin));
    partial void OnLoginChanged(PersonLoginDto? value) => OnPropertyChanged(nameof(ShowCreateLogin));
}

/// <summary>Mirrors the web's Owner/Tenant flat detail screen — the
/// occupancy panel (members + add/edit/remove), documents, login accounts,
/// emergency contacts, vehicles, and occupancy history, all scoped to one
/// flat. Reached by tapping a row in ResidentsPage's Owner/Tenant tab.</summary>
public partial class FlatResidentDetailViewModel : ObservableObject
{
    private readonly FlatOccupanciesClient _occupanciesClient;
    private readonly ResidentDocumentsClient _documentsClient;
    private readonly EmergencyContactsClient _contactsClient;
    private readonly VehiclesClient _vehiclesClient;
    private readonly PersonsClient _personsClient;
    private readonly RolesClient _rolesClient;
    private readonly CurrentSocietyService _currentSocietyService;
    private List<RoleDto> _rolesCache = new();

    public FlatResidentDetailViewModel(
        FlatOccupanciesClient occupanciesClient, ResidentDocumentsClient documentsClient, EmergencyContactsClient contactsClient,
        VehiclesClient vehiclesClient, PersonsClient personsClient, RolesClient rolesClient, CurrentSocietyService currentSocietyService)
    {
        _occupanciesClient = occupanciesClient;
        _documentsClient = documentsClient;
        _contactsClient = contactsClient;
        _vehiclesClient = vehiclesClient;
        _personsClient = personsClient;
        _rolesClient = rolesClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private int flatId;
    [ObservableProperty] private string flatNumber = string.Empty;
    /// <summary>"Owner" or "Tenant" — which of FlatOccupancyOverviewDto's two
    /// current episodes this page features, matching the web's separate
    /// Owner/Tenant detail routes.</summary>
    [ObservableProperty] private string occupancyType = "Owner";

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public string PageTitle => $"Flat {FlatNumber} — {OccupancyType}";

    // ==================== Occupancy panel ====================
    [ObservableProperty] private FlatOccupancyDto? occupancy;
    [ObservableProperty] private bool hasOccupancy;

    async partial void OnFlatIdChanged(int value)
    {
        if (value > 0) await LoadAllAsync();
    }

    [RelayCommand]
    private async Task LoadAllAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var overviewResponse = await _occupanciesClient.Overview2Async(FlatId);
            var overview = overviewResponse.Data;
            Occupancy = OccupancyType == "Tenant" ? overview?.CurrentTenantOccupancy : overview?.CurrentOwnerOccupancy;
            HasOccupancy = Occupancy is not null;

            await LoadLoginsAsync();
            await LoadDocumentsAsync();
            await LoadEmergencyContactsAsync();
            await LoadVehiclesAsync();
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load this flat's details ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddMemberAsync()
    {
        if (Shell.Current is null) return;

        var mode = OccupancyType == "Owner" ? "OwnerMember" : Occupancy is null ? "TenantNew" : "TenantFamily";
        var query = new Dictionary<string, object> { ["mode"] = mode, ["flatId"] = FlatId };
        if (Occupancy?.Id is int occId) query["flatOccupancyId"] = occId;
        await Shell.Current.GoToAsync(nameof(OccupancyMemberFormPage), query);
    }

    [RelayCommand]
    private async Task EditMemberAsync(OccupancyMemberDto member)
    {
        if (Shell.Current is null || member.PersonId is not int personId) return;
        await Shell.Current.GoToAsync(nameof(OccupancyMemberFormPage), new Dictionary<string, object> { ["personId"] = personId });
    }

    [RelayCommand]
    private async Task RemoveMemberAsync(OccupancyMemberDto member)
    {
        if (Shell.Current is null || member.Id is not int memberId) return;

        var confirmed = await Shell.Current.DisplayAlert("Remove Member", $"Remove {member.PersonName} from this flat?", "Remove", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _occupanciesClient.RemoveAsync(memberId, new RemoveOccupancyMemberRequest { LeftDate = DateTime.Today });
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't remove this member ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Documents ====================
    [ObservableProperty] private ObservableCollection<ResidentDocumentDto> documents = new();

    private async Task LoadDocumentsAsync()
    {
        if (Occupancy?.Id is not int occId) { Documents = new(); return; }
        var response = await _documentsClient.ResidentDocumentsGETAsync(occId);
        Documents = new ObservableCollection<ResidentDocumentDto>(response.Data ?? new());
    }

    [RelayCommand]
    private async Task UploadDocumentAsync()
    {
        if (Shell.Current is null || Occupancy?.Id is not int occId) return;

        var typeChoice = await Shell.Current.DisplayActionSheet(
            "Document Type", "Cancel", null, "Possession Letter", "Parking Allotment Letter", "Tenant Police NOC", "Rental Agreement", "Other");
        var type = typeChoice switch
        {
            "Possession Letter" => ResidentDocumentType.PossessionLetter,
            "Parking Allotment Letter" => ResidentDocumentType.ParkingAllotmentLetter,
            "Tenant Police NOC" => ResidentDocumentType.TenantPoliceNoc,
            "Rental Agreement" => ResidentDocumentType.RentalAgreement,
            "Other" => ResidentDocumentType.Other,
            _ => (ResidentDocumentType?)null
        };
        if (type is null) return;

        var url = await Shell.Current.DisplayPromptAsync("Document URL", "Paste the document's URL");
        if (string.IsNullOrWhiteSpace(url)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _documentsClient.ResidentDocumentsPOSTAsync(new UploadResidentDocumentCommand
            {
                FlatOccupancyId = occId, DocumentType = type, DocumentUrl = url
            });
            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't upload this document ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync(ResidentDocumentDto document)
    {
        if (Shell.Current is null || document.Id is not int id) return;
        if (!await Shell.Current.DisplayAlert("Delete Document", "Delete this document?", "Delete", "Cancel")) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _documentsClient.ResidentDocumentsDELETEAsync(id);
            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete this document ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Login Accounts ====================
    [ObservableProperty] private ObservableCollection<MemberLoginItemViewModel> logins = new();

    private async Task LoadLoginsAsync()
    {
        var members = Occupancy?.Members ?? new();
        var items = members.Select(m => new MemberLoginItemViewModel(m)).ToList();
        Logins = new ObservableCollection<MemberLoginItemViewModel>(items);

        foreach (var item in items)
        {
            try
            {
                var response = await _personsClient.LoginGETAsync(item.PersonId);
                item.Login = response.Data;
            }
            catch
            {
                // Best-effort — one member's lookup failing shouldn't block the rest.
            }
            finally
            {
                item.IsChecking = false;
            }
        }
    }

    [RelayCommand]
    private async Task CreateLoginAsync(MemberLoginItemViewModel item)
    {
        if (Shell.Current is null) return;

        if (_rolesCache.Count == 0)
        {
            try
            {
                var rolesResponse = await _rolesClient.RolesGETAsync();
                _rolesCache = (rolesResponse.Data ?? new()).ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Couldn't load roles ({ex.Message}).";
                return;
            }
        }

        var roleNames = _rolesCache.Select(r => r.Name ?? "—").ToArray();
        if (roleNames.Length == 0) { ErrorMessage = "No roles are configured."; return; }

        var roleChoice = await Shell.Current.DisplayActionSheet($"Role for {item.PersonName}", "Cancel", null, roleNames);
        var role = _rolesCache.FirstOrDefault(r => r.Name == roleChoice);
        if (role?.Id is not int roleId) return;

        var password = await Shell.Current.DisplayPromptAsync(
            "Password", "Leave blank to use the default (Test@12345)", "Create", "Cancel");

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _personsClient.CreateLogin2Async(item.PersonId, new CreatePersonLoginRequest
            {
                FlatId = FlatId, RoleId = roleId, Password = string.IsNullOrWhiteSpace(password) ? null : password
            });
            var response = await _personsClient.LoginGETAsync(item.PersonId);
            item.Login = response.Data;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't create the login ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Emergency Contacts ====================
    [ObservableProperty] private ObservableCollection<EmergencyContactDto> emergencyContacts = new();

    private async Task LoadEmergencyContactsAsync()
    {
        var response = await _contactsClient.EmergencyContactsGETAsync(FlatId);
        EmergencyContacts = new ObservableCollection<EmergencyContactDto>(response.Data ?? new());
    }

    [RelayCommand]
    private async Task AddEmergencyContactAsync()
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(EmergencyContactFormPage), new Dictionary<string, object> { ["flatId"] = FlatId });
    }

    [RelayCommand]
    private async Task EditEmergencyContactAsync(EmergencyContactDto contact)
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(EmergencyContactFormPage), new Dictionary<string, object> { ["contact"] = contact });
    }

    [RelayCommand]
    private async Task DeleteEmergencyContactAsync(EmergencyContactDto contact)
    {
        if (Shell.Current is null || contact.Id is not int id) return;
        if (!await Shell.Current.DisplayAlert("Delete Contact", $"Delete {contact.ContactName}?", "Delete", "Cancel")) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _contactsClient.EmergencyContactsDELETEAsync(id);
            await LoadEmergencyContactsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete this contact ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Vehicles ====================
    [ObservableProperty] private ObservableCollection<VehicleDto> vehicles = new();

    private async Task LoadVehiclesAsync()
    {
        var response = await _vehiclesClient.VehiclesGETAsync(null, FlatId, null, null, null, false, 1, 100);
        Vehicles = new ObservableCollection<VehicleDto>(response.Data?.Items ?? new());
    }

    [RelayCommand]
    private async Task AddVehicleAsync()
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(VehicleFormPage), new Dictionary<string, object> { ["flatId"] = FlatId });
    }

    [RelayCommand]
    private async Task EditVehicleAsync(VehicleDto vehicle)
    {
        if (Shell.Current is null) return;
        await Shell.Current.GoToAsync(nameof(VehicleFormPage), new Dictionary<string, object> { ["vehicle"] = vehicle });
    }

    [RelayCommand]
    private async Task DeleteVehicleAsync(VehicleDto vehicle)
    {
        if (Shell.Current is null || vehicle.Id is not int id) return;
        if (!await Shell.Current.DisplayAlert("Delete Vehicle", $"Delete {vehicle.RegistrationNumber}?", "Delete", "Cancel")) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _vehiclesClient.VehiclesDELETEAsync(id);
            await LoadVehiclesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't delete this vehicle ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Occupancy History ====================
    [ObservableProperty] private ObservableCollection<FlatOccupancyDto> history = new();

    private async Task LoadHistoryAsync()
    {
        var response = await _occupanciesClient.HistoryAsync(FlatId, null);
        History = new ObservableCollection<FlatOccupancyDto>(response.Data ?? new());
    }
}
