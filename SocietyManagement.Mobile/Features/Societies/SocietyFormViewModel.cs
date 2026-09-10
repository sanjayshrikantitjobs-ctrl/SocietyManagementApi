using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Features.Societies;

/// <summary>Add/Edit Society — mirrors the web's "Edit Society" dialog
/// (logo/name/registration/address/city/state/pincode/contact/code, plus a
/// Super-Admin-only subscription-extend + restrict section). The quick
/// +30/+60/+1 year buttons and the Restrict checkbox call their own
/// endpoints (SetSubscription/SetSuspension) immediately rather than
/// waiting for Save — those are separate commands from UpdateSocietyCommand
/// on the backend, gated to Super Admin only (see SocietyCommands.cs's own
/// doc comments), same split the web keeps.</summary>
public partial class SocietyFormViewModel : ObservableObject
{
    private readonly SocietiesClient _client;
    private bool _suppressSuspensionCallback;

    public SocietyFormViewModel(SocietiesClient client, AuthState authState)
    {
        _client = client;
        Auth = authState;
    }

    public AuthState Auth { get; }

    [ObservableProperty] private int id;
    [ObservableProperty] private string? logoUrl;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string? registrationNumber;
    [ObservableProperty] private string address = string.Empty;
    [ObservableProperty] private string city = string.Empty;
    [ObservableProperty] private string state = string.Empty;
    [ObservableProperty] private string pincode = string.Empty;
    [ObservableProperty] private string? contactEmail;
    [ObservableProperty] private string? contactPhone;
    [ObservableProperty] private string? code;
    [ObservableProperty] private DateTime subscriptionEndDate = DateTime.Today;
    [ObservableProperty] private bool isSuspended;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    async partial void OnIdChanged(int value)
    {
        if (value > 0) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var response = await _client.SocietiesGET2Async(Id);
            var society = response.Data;
            if (society is null) return;

            LogoUrl = society.LogoUrl;
            Name = society.Name ?? string.Empty;
            RegistrationNumber = society.RegistrationNumber;
            Address = society.Address ?? string.Empty;
            City = society.City ?? string.Empty;
            State = society.State ?? string.Empty;
            Pincode = society.Pincode ?? string.Empty;
            ContactEmail = society.ContactEmail;
            ContactPhone = society.ContactPhone;
            Code = society.Code;
            if (society.SubscriptionEndDate is DateTimeOffset end) SubscriptionEndDate = end.Date;
            _suppressSuspensionCallback = true;
            IsSuspended = society.IsSubscriptionSuspended ?? false;
            _suppressSuspensionCallback = false;
            OnPropertyChanged(nameof(IsEditMode));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load this society ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(City)
            || string.IsNullOrWhiteSpace(State) || string.IsNullOrWhiteSpace(Pincode))
        {
            ErrorMessage = "Name, address, city, state and pincode are required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.SocietiesPUTAsync(Id, new UpdateSocietyCommand
                {
                    Id = Id, Name = Name, RegistrationNumber = RegistrationNumber, Address = Address, City = City,
                    State = State, Pincode = Pincode, ContactEmail = ContactEmail, ContactPhone = ContactPhone,
                    LogoUrl = LogoUrl, Code = Code
                });
            }
            else
            {
                // No dedicated Add-Society screenshot to match — a new
                // society defaults to a one-year subscription starting
                // today; extending/restricting it is done afterwards from
                // the Edit dialog's Super-Admin-only section.
                await _client.SocietiesPOSTAsync(new CreateSocietyCommand
                {
                    Name = Name, RegistrationNumber = RegistrationNumber, Address = Address, City = City, State = State,
                    Pincode = Pincode, ContactEmail = ContactEmail, ContactPhone = ContactPhone, LogoUrl = LogoUrl,
                    SubscriptionStartDate = DateTime.Today, SubscriptionEndDate = DateTime.Today.AddYears(1)
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save this society ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExtendSubscriptionAsync(string days)
    {
        if (!IsEditMode) return;

        var newEnd = days switch
        {
            "30" => SubscriptionEndDate.AddDays(30),
            "60" => SubscriptionEndDate.AddDays(60),
            "365" => SubscriptionEndDate.AddYears(1),
            _ => SubscriptionEndDate
        };

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _client.SubscriptionAsync(Id, new SetSocietySubscriptionCommand
            {
                Id = Id, SubscriptionStartDate = DateTime.Today, SubscriptionEndDate = newEnd
            });
            SubscriptionEndDate = newEnd;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't extend the subscription ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsSuspendedChanged(bool value)
    {
        if (_suppressSuspensionCallback) return;
        _ = ApplySuspensionAsync(value);
    }

    /// <summary>Programmatically reverts IsSuspended (confirm declined, or
    /// the API call failed) without re-entering this same handler.</summary>
    private void RevertIsSuspended(bool value)
    {
        _suppressSuspensionCallback = true;
        IsSuspended = value;
        _suppressSuspensionCallback = false;
    }

    private async Task ApplySuspensionAsync(bool suspend)
    {
        if (!IsEditMode || Shell.Current is null) return;

        if (suspend)
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "Restrict Society", "This blocks every user of this society from logging in immediately. Continue?", "Restrict", "Cancel");
            if (!confirmed)
            {
                RevertIsSuspended(false);
                return;
            }
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _client.SuspensionAsync(Id, new SetSocietySuspensionCommand { Id = Id, IsSuspended = suspend });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update the restriction ({ex.Message}).";
            RevertIsSuspended(!suspend);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
