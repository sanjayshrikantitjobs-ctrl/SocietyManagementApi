using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

/// <summary>Mirrors festival-sponsors-tab.component.ts's Add/Edit dialog.
/// SponsorshipType is no longer collected via the form on web either — a
/// defunct-but-still-required backend field — so it's preserved silently:
/// Title(1) on create, the sponsor's existing value on edit.</summary>
public partial class SponsorFormViewModel : ObservableObject
{
    private readonly FestivalSponsorsClient _client;

    public SponsorFormViewModel(FestivalSponsorsClient client)
    {
        _client = client;
    }

    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int id;
    [ObservableProperty] private SponsorshipType sponsorshipType = SponsorshipType.Title;
    [ObservableProperty] private string companyName = string.Empty;
    [ObservableProperty] private string? contactPerson;
    [ObservableProperty] private string? phone;
    [ObservableProperty] private string? email;
    [ObservableProperty] private decimal promisedAmount;
    [ObservableProperty] private decimal receivedAmount;
    [ObservableProperty] private string? logoUrl;
    [ObservableProperty] private string? bannerUrl;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    public void LoadFrom(FestivalSponsorDto sponsor)
    {
        Id = sponsor.Id ?? 0;
        SponsorshipType = sponsor.SponsorshipType ?? SponsorshipType.Title;
        CompanyName = sponsor.CompanyName ?? string.Empty;
        ContactPerson = sponsor.ContactPerson;
        Phone = sponsor.Phone;
        Email = sponsor.Email;
        PromisedAmount = (decimal)(sponsor.PromisedAmount ?? 0);
        ReceivedAmount = (decimal)(sponsor.ReceivedAmount ?? 0);
        LogoUrl = sponsor.LogoUrl;
        BannerUrl = sponsor.BannerUrl;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            ErrorMessage = "Company name is required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.FestivalSponsorsPUTAsync(Id, new UpdateSponsorCommand
                {
                    Id = Id, CompanyName = CompanyName, ContactPerson = ContactPerson, Phone = Phone, Email = Email,
                    SponsorshipType = SponsorshipType, PromisedAmount = (double)PromisedAmount, ReceivedAmount = (double)ReceivedAmount,
                    LogoUrl = LogoUrl, BannerUrl = BannerUrl
                });
            }
            else
            {
                await _client.FestivalSponsorsPOSTAsync(new CreateSponsorCommand
                {
                    FestivalId = FestivalId, CompanyName = CompanyName, ContactPerson = ContactPerson, Phone = Phone, Email = Email,
                    SponsorshipType = SponsorshipType.Title, PromisedAmount = (double)PromisedAmount, ReceivedAmount = (double)ReceivedAmount,
                    LogoUrl = LogoUrl, BannerUrl = BannerUrl
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the sponsor ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
