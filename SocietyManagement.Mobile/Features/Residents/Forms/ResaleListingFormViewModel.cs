using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Residents.Forms;

public record ListingMemberOption(string Label, int Value);

/// <summary>Add/Edit a flat resale listing. ListedByMemberId is the
/// backend's legacy Member id (FlatResaleListingFeature.cs validates
/// against _context.Members, not the newer Person/Occupancy model) — the
/// member picker below is every legacy Member on file for this society,
/// same limitation the web itself inherits from that table.</summary>
public partial class ResaleListingFormViewModel : ObservableObject
{
    private readonly FlatResaleListingsClient _client;
    private readonly MembersClient _membersClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public ResaleListingFormViewModel(FlatResaleListingsClient client, MembersClient membersClient, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _membersClient = membersClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private int id;
    [ObservableProperty] private int flatId;
    [ObservableProperty] private ObservableCollection<ListingMemberOption> memberOptions = new();
    [ObservableProperty] private ListingMemberOption? selectedMember;
    [ObservableProperty] private decimal askingPrice;
    [ObservableProperty] private DateTime availableFrom = DateTime.Today;
    [ObservableProperty] private string? visitTimingNotes;
    [ObservableProperty] private bool nocRequested;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    public async Task LoadMemberOptionsAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) return;

        try
        {
            var response = await _membersClient.MembersGET4Async(societyId, null, null, false, 1, 500);
            MemberOptions = new ObservableCollection<ListingMemberOption>(
                (response.Data?.Items ?? new()).Select(m => new ListingMemberOption($"{m.FirstName} {m.LastName}".Trim(), m.Id ?? 0)));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load members ({ex.Message}).";
        }
    }

    /// <summary>No GetById is wired up on mobile — the row tapped from the
    /// Resale Listings tab is passed straight through instead.</summary>
    public void LoadFrom(FlatResaleListingDto listing)
    {
        Id = listing.Id ?? 0;
        FlatId = listing.FlatId ?? 0;
        AskingPrice = (decimal)(listing.AskingPrice ?? 0);
        if (listing.AvailableFrom is DateTimeOffset date) AvailableFrom = date.Date;
        VisitTimingNotes = listing.VisitTimingNotes;
        NocRequested = listing.NocRequested ?? false;
        Notes = listing.Notes;
        SelectedMember = MemberOptions.FirstOrDefault(o => o.Value == listing.ListedByMemberId);
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (AskingPrice <= 0)
        {
            ErrorMessage = "Enter an asking price greater than zero.";
            return;
        }
        if (!IsEditMode && SelectedMember is null)
        {
            ErrorMessage = "Pick who is listing this flat.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.FlatResaleListingsPUTAsync(Id, new UpdateListingCommand
                {
                    Id = Id, AskingPrice = (double)AskingPrice, AvailableFrom = AvailableFrom,
                    VisitTimingNotes = VisitTimingNotes, NocRequested = NocRequested, Notes = Notes
                });
            }
            else
            {
                await _client.FlatResaleListingsPOSTAsync(new CreateListingCommand
                {
                    FlatId = FlatId, ListedByMemberId = SelectedMember!.Value, AskingPrice = (double)AskingPrice,
                    AvailableFrom = AvailableFrom, VisitTimingNotes = VisitTimingNotes, NocRequested = NocRequested, Notes = Notes
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save this listing ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
