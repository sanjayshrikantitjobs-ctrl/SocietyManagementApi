using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Features.Festivals.Forms;

namespace SocietyManagement.Mobile.Features.Festivals;

public record DistributionClaimStatusOption(string Label, DistributionClaimStatus? Value);

/// <summary>One flat's claim slots, grouped client-side from the flat
/// unfiltered claim list — mirrors festival-distribution-detail-dialog.component.ts's
/// ClaimFlatGroup/groupedClaims computed signal.</summary>
public class ClaimFlatGroupVm
{
    public int FlatId { get; init; }
    public string FlatNumber { get; init; } = string.Empty;
    public List<DistributionClaimDto> Claims { get; init; } = new();
    public bool AllDistributed { get; init; }
    public bool HasDistributed { get; init; }
}

/// <summary>Mirrors festival-distribution-detail-dialog.component.ts — the
/// full dashboard + claim machinery for one distribution: eligibility
/// generation, variant management, the admin claims table (grouped by
/// flat), and a self-service "My Items" section. A pushed page rather than
/// a modal dialog (MAUI has no lightweight equivalent), same choice as
/// FlatContributionDetailPage/BudgetRevisionsPage elsewhere in this module.
/// No canManage/canSelect permission gate — this ViewModel's sibling tabs
/// (Sponsors, Vendors, Budget, ...) don't gate their manage actions on
/// mobile either; the backend enforces authorization on every action
/// regardless of what the UI shows.</summary>
public partial class DistributionDetailViewModel : ObservableObject
{
    private readonly FestivalDistributionsClient _client;
    private readonly FlatsClient _flatsClient;
    private readonly CurrentSocietyService _currentSocietyService;

    private static readonly DistributionClaimStatusOption[] StatusOptionsSeed =
    {
        new("All Statuses", null),
        new("Pending", DistributionClaimStatus.Pending),
        new("Confirmed", DistributionClaimStatus.Confirmed),
        new("Distributed", DistributionClaimStatus.Distributed),
    };

    public DistributionDetailViewModel(FestivalDistributionsClient client, FlatsClient flatsClient, CurrentSocietyService currentSocietyService)
    {
        _client = client;
        _flatsClient = flatsClient;
        _currentSocietyService = currentSocietyService;
        selectedStatus = StatusOptionsSeed[0];
    }

    public DistributionClaimStatusOption[] StatusOptions => StatusOptionsSeed;

    [ObservableProperty] private int distributionId;
    [ObservableProperty] private FestivalDistributionDetailDto? distribution;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string flatSearch = string.Empty;
    [ObservableProperty] private DistributionClaimStatusOption selectedStatus;
    [ObservableProperty] private ObservableCollection<ClaimFlatGroupVm> groupedClaims = new();
    [ObservableProperty] private ObservableCollection<DistributionClaimDto> myClaims = new();
    [ObservableProperty] private int totalItemCount;

    private List<DistributionClaimDto> _allClaims = new();
    private List<FlatDto> _flatsCache = new();

    public string EligibilityDescription => Distribution?.EligibilityType switch
    {
        DistributionEligibilityType.ContributionPaid => "Flats that have partially or fully paid their contribution are eligible.",
        DistributionEligibilityType.MinimumAmount => $"Flats that have contributed ₹{Distribution?.EligibilityMinContribution:N0}+ toward this festival are eligible.",
        _ => "Every flat is eligible."
    };

    partial void OnFlatSearchChanged(string value) => ApplyFilter();
    partial void OnSelectedStatusChanged(DistributionClaimStatusOption value) => ApplyFilter();
    partial void OnDistributionChanged(FestivalDistributionDetailDto? value) => OnPropertyChanged(nameof(EligibilityDescription));

    async partial void OnDistributionIdChanged(int value)
    {
        if (value > 0) await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (DistributionId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.FestivalDistributionsGET2Async(DistributionId);
            Distribution = response.Data;

            var claimsResponse = await _client.ClaimsAsync(DistributionId, null, null);
            _allClaims = (claimsResponse.Data ?? new()).ToList();
            ApplyFilter();

            var myClaimsResponse = await _client.MyClaimsAsync(DistributionId);
            MyClaims = new ObservableCollection<DistributionClaimDto>(myClaimsResponse.Data ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load this distribution ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var search = FlatSearch.Trim();
        var status = SelectedStatus.Value;
        var filtered = _allClaims.Where(c =>
            (string.IsNullOrEmpty(search) || (c.FlatNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (status is null || c.Status == status)).ToList();

        TotalItemCount = filtered.Count;

        var groups = filtered
            .GroupBy(c => c.FlatId)
            .Select(g => new ClaimFlatGroupVm
            {
                FlatId = g.Key ?? 0,
                FlatNumber = g.FirstOrDefault()?.FlatNumber ?? "—",
                Claims = g.OrderBy(c => c.SlotNumber).ToList(),
                AllDistributed = g.All(c => c.Status == DistributionClaimStatus.Distributed),
                HasDistributed = g.Any(c => c.Status == DistributionClaimStatus.Distributed)
            })
            .OrderBy(g => g.FlatNumber, StringComparer.OrdinalIgnoreCase);

        GroupedClaims = new ObservableCollection<ClaimFlatGroupVm>(groups);
    }

    [RelayCommand]
    private async Task EditDistributionAsync()
    {
        var d = Distribution;
        if (d is null || Shell.Current is null) return;

        await Shell.Current.GoToAsync(nameof(DistributionFormPage), new Dictionary<string, object>
        {
            ["distribution"] = new FestivalDistributionDto
            {
                Id = d.Id, ItemName = d.ItemName, Description = d.Description, EligibilityType = d.EligibilityType,
                EligibilityMinContribution = d.EligibilityMinContribution, QuantityPerFlat = d.QuantityPerFlat
            }
        });
    }

    [RelayCommand]
    private async Task AddVariantAsync()
    {
        if (Shell.Current is null) return;
        var label = await Shell.Current.DisplayPromptAsync("Add Variant", "Label (e.g. L, XL, XXL)");
        if (string.IsNullOrWhiteSpace(label)) return;

        try
        {
            await _client.VariantsPOSTAsync(DistributionId, new AddVariantRequest { Label = label });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't add the variant ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task RemoveVariantAsync(FestivalDistributionVariantDto variant)
    {
        try
        {
            await _client.VariantsDELETEAsync(variant.Id ?? 0);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't remove the variant ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task GenerateEligibilityAsync()
    {
        if (Shell.Current is null) return;
        var confirmed = await Shell.Current.DisplayAlert("Generate Eligibility",
            "Find every newly-eligible flat and create its claim slots? Flats already processed are skipped.", "Generate", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var response = await _client.GenerateAsync(DistributionId);
            await Shell.Current.DisplayAlert("Generate Eligibility", $"{response.Data} slot(s) generated.", "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't generate eligibility ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddFlatAsync()
    {
        if (Shell.Current is null) return;

        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null) { ErrorMessage = "No society available for this account."; return; }

        var flatNumber = await Shell.Current.DisplayPromptAsync("Add Flat", "Flat number");
        if (string.IsNullOrWhiteSpace(flatNumber)) return;

        if (_flatsCache.Count == 0)
        {
            var flatsResponse = await _flatsClient.FlatsGETAsync(null, null, null, societyId, 1, 500);
            _flatsCache = (flatsResponse.Data?.Items ?? new()).ToList();
        }

        var flat = _flatsCache.FirstOrDefault(f => string.Equals(f.FlatNumber, flatNumber, StringComparison.OrdinalIgnoreCase));
        if (flat?.Id is not int flatId)
        {
            ErrorMessage = $"No flat numbered \"{flatNumber}\" was found in this society.";
            return;
        }

        var quantityText = await Shell.Current.DisplayPromptAsync("Add Flat", "Quantity", initialValue: "1", keyboard: Keyboard.Numeric);
        if (!int.TryParse(quantityText, out var quantity) || quantity <= 0) return;

        try
        {
            await _client.FlatsPOSTAsync(DistributionId, flatId, new AddManualClaimRequest { Quantity = quantity });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't add the flat ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task MarkFlatDistributedAsync(ClaimFlatGroupVm group)
    {
        if (Shell.Current is null) return;
        var confirmed = await Shell.Current.DisplayAlert("Mark All Distributed",
            $"Confirm all {group.Claims.Count} item(s) for Flat {group.FlatNumber} have been handed over?", "Confirm", "Cancel");
        if (!confirmed) return;

        try
        {
            await _client.Distribute2Async(DistributionId, group.FlatId);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't mark the flat distributed ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task RemoveFlatAsync(ClaimFlatGroupVm group)
    {
        if (Shell.Current is null) return;
        var confirmed = await Shell.Current.DisplayAlert("Not Qualified",
            $"Remove Flat {group.FlatNumber} from this distribution? Its {group.Claims.Count} pending/confirmed slot(s) will be deleted.", "Remove", "Cancel");
        if (!confirmed) return;

        try
        {
            await _client.FlatsDELETEAsync(DistributionId, group.FlatId);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't remove the flat ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task AddExtraAsync(ClaimFlatGroupVm group)
    {
        if (Shell.Current is null) return;

        var quantityText = await Shell.Current.DisplayPromptAsync("Add Extra (Paid)", $"Quantity for Flat {group.FlatNumber}", initialValue: "1", keyboard: Keyboard.Numeric);
        if (!int.TryParse(quantityText, out var quantity) || quantity <= 0) return;

        var amountText = await Shell.Current.DisplayPromptAsync("Add Extra (Paid)", "Amount per unit (₹)", keyboard: Keyboard.Numeric);
        if (!double.TryParse(amountText, out var amount) || amount <= 0) return;

        var notes = await Shell.Current.DisplayPromptAsync("Add Extra (Paid)", "Notes (optional) — adds a Special Charge in Maintenance");

        try
        {
            await _client.ExtraAsync(DistributionId, group.FlatId, new AddChargeableExtraRequest
            {
                Quantity = quantity, AmountPerUnit = amount, Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
            });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't add the extra item ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task MarkDistributedAsync(DistributionClaimDto claim)
    {
        if (Shell.Current is null) return;
        var confirmed = await Shell.Current.DisplayAlert("Mark as Distributed",
            $"Confirm this item has been physically handed over for Flat {claim.FlatNumber}?", "Confirm", "Cancel");
        if (!confirmed) return;

        try
        {
            await _client.DistributeAsync(claim.Id ?? 0);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't mark the item distributed ({ex.Message}).";
        }
    }

    /// <summary>Web's "Assign Member"/"Select Item" dialogs offer a member
    /// picker plus a "+ Add New Member…" option in one form; MAUI has no
    /// built-in multi-field dialog, so this chains two native prompts
    /// instead — an ActionSheet to pick (or add) a member, matching the
    /// same chained-dialog pattern CreateLoginAsync already uses elsewhere
    /// in this app.</summary>
    private async Task<int?> PromptAssignMemberIdAsync(int flatId, string flatNumber, int? currentMemberId)
    {
        if (Shell.Current is null) return currentMemberId;

        List<FlatMemberOptionDto> members;
        try
        {
            var response = await _client.MembersGETAsync(flatId);
            members = (response.Data ?? new()).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load flat members ({ex.Message}).";
            return currentMemberId;
        }

        const string unassigned = "— Unassigned —";
        const string addNew = "+ Add New Member…";
        var options = new List<string> { unassigned };
        options.AddRange(members.Select(m => m.Name ?? "—"));
        options.Add(addNew);

        var choice = await Shell.Current.DisplayActionSheet($"Assign Member — Flat {flatNumber}", "Cancel", null, options.ToArray());
        if (choice is null || choice == "Cancel") return currentMemberId;
        if (choice == addNew) return await PromptAddResidentAsync(flatId, flatNumber);
        if (choice == unassigned) return null;

        return members.FirstOrDefault(m => m.Name == choice)?.MemberId;
    }

    /// <summary>Adds a person to the flat's household via the real Occupancy
    /// model (so they show up everywhere else too, not just this claim),
    /// mirroring the web's promptAddResident.</summary>
    private async Task<int?> PromptAddResidentAsync(int flatId, string flatNumber)
    {
        if (Shell.Current is null) return null;

        var firstName = await Shell.Current.DisplayPromptAsync($"Add Member — Flat {flatNumber}", "First Name");
        if (string.IsNullOrWhiteSpace(firstName)) return null;
        var lastName = await Shell.Current.DisplayPromptAsync($"Add Member — Flat {flatNumber}", "Last Name");
        if (string.IsNullOrWhiteSpace(lastName)) return null;
        var phone = await Shell.Current.DisplayPromptAsync($"Add Member — Flat {flatNumber}", "Phone (optional)");

        var relationshipChoice = await Shell.Current.DisplayActionSheet("Relationship", "Cancel", null,
            "Self", "Spouse", "Son", "Daughter", "Parent", "Grandparent", "Sibling", "Other");
        var relationship = relationshipChoice switch
        {
            "Self" => PersonRelationship.Self,
            "Spouse" => PersonRelationship.Spouse,
            "Son" => PersonRelationship.Son,
            "Daughter" => PersonRelationship.Daughter,
            "Parent" => PersonRelationship.Parent,
            "Grandparent" => PersonRelationship.Grandparent,
            "Sibling" => PersonRelationship.Sibling,
            _ => PersonRelationship.Other
        };

        try
        {
            var response = await _client.ResidentsAsync(flatId, new AddFlatResidentRequest
            {
                FirstName = firstName, LastName = lastName, Phone = string.IsNullOrWhiteSpace(phone) ? null : phone, Relationship = relationship
            });
            return response.Data?.MemberId;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't add that member ({ex.Message}).";
            return null;
        }
    }

    /// <summary>Mirrors the web's per-slot "⋮" menu (Assign Member / Select
    /// Variant / Mark as Distributed) as a single native ActionSheet.</summary>
    [RelayCommand]
    private async Task ShowClaimActionsAsync(DistributionClaimDto claim)
    {
        if (Shell.Current is null) return;

        var options = new List<string> { "Assign Member", "Select Variant" };
        if (claim.Status != DistributionClaimStatus.Distributed) options.Add("Mark as Distributed");

        var choice = await Shell.Current.DisplayActionSheet($"Slot #{claim.SlotNumber} — Flat {claim.FlatNumber}", "Cancel", null, options.ToArray());
        switch (choice)
        {
            case "Assign Member": await AssignMemberAsync(claim); break;
            case "Select Variant": await SelectVariantAsync(claim); break;
            case "Mark as Distributed": await MarkDistributedAsync(claim); break;
        }
    }

    [RelayCommand]
    private async Task AssignMemberAsync(DistributionClaimDto claim)
    {
        if (claim.FlatId is not int flatId) return;
        var memberId = await PromptAssignMemberIdAsync(flatId, claim.FlatNumber ?? "—", claim.MemberId);

        try
        {
            await _client.AssignMemberAsync(claim.Id ?? 0, new AssignClaimMemberRequest { MemberId = memberId });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update the assignment ({ex.Message}).";
        }
    }

    [RelayCommand]
    private async Task SelectVariantAsync(DistributionClaimDto claim)
    {
        if (Shell.Current is null || claim.FlatId is not int flatId) return;

        int? variantId = claim.VariantId;
        var variants = Distribution?.Variants ?? new();
        if (variants.Count > 0)
        {
            var labels = variants.Select(v => v.Label ?? "—").ToArray();
            var choice = await Shell.Current.DisplayActionSheet($"Select Item — Flat {claim.FlatNumber}", "Cancel", null, labels);
            if (choice is null || choice == "Cancel") return;
            variantId = variants.FirstOrDefault(v => v.Label == choice)?.Id;
        }

        var memberId = await PromptAssignMemberIdAsync(flatId, claim.FlatNumber ?? "—", claim.MemberId);

        try
        {
            await _client.SelectAsync(claim.Id ?? 0, new SelectClaimVariantRequest { VariantId = variantId, MemberId = memberId });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the selection ({ex.Message}).";
        }
    }
}
