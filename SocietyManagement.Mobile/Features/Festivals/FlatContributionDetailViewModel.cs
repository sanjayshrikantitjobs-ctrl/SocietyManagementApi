using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Features.Festivals.Forms;

namespace SocietyManagement.Mobile.Features.Festivals;

/// <summary>Mirrors flat-contribution-detail-dialog.component.ts — one
/// flat's target/paid/outstanding plus its full contribution history, with
/// Edit Target and Add Contribution actions (both gated by canManage/
/// canContribute on web; mobile shows them unconditionally since role
/// gating for this screen isn't in scope yet, same simplification already
/// used elsewhere in this app).</summary>
public partial class FlatContributionDetailViewModel : ObservableObject
{
    private readonly FestivalContributionsClient _client;

    public FlatContributionDetailViewModel(FestivalContributionsClient client)
    {
        _client = client;
    }

    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int flatId;
    [ObservableProperty] private string flatNumber = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private FlatContributionDto? summary;
    [ObservableProperty] private ObservableCollection<FestivalContributionDto> contributions = new();

    async partial void OnFlatIdChanged(int value) => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (FlatId <= 0) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var summaryResponse = await _client.FlatSummaryAsync(FestivalId, FlatNumber, null, null, false, 1, 1);
            Summary = summaryResponse.Data?.Items?.FirstOrDefault(f => f.FlatId == FlatId);

            var response = await _client.FestivalContributionsGETAsync(FestivalId, FlatId, null, null, null, false, 1, 100);
            Contributions = new ObservableCollection<FestivalContributionDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load this flat's contributions ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditTargetAsync()
    {
        if (Shell.Current is null) return;

        var input = await Shell.Current.DisplayPromptAsync("Edit Target", $"Target for Flat {FlatNumber} (₹)",
            "Save", "Cancel", initialValue: (Summary?.TargetAmount ?? 0).ToString("0"), keyboard: Keyboard.Numeric);
        if (input is null || !double.TryParse(input, out var amount)) return;

        IsBusy = true;
        try
        {
            await _client.TargetsPUTAsync(new UpdateFlatContributionTargetCommand { FestivalId = FestivalId, FlatId = FlatId, TargetAmount = amount });
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't update the target ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddContributionAsync()
    {
        await Shell.Current.GoToAsync(nameof(ContributionFormPage),
            new Dictionary<string, object> { ["festivalId"] = FestivalId, ["lockedFlatId"] = FlatId, ["lockedFlatNumber"] = FlatNumber });
    }

    [RelayCommand]
    private async Task ShowContributionActionsAsync(FestivalContributionDto contribution)
    {
        if (Shell.Current is null) return;

        var choice = await Shell.Current.DisplayActionSheet(contribution.ReceiptNumber, "Cancel", null, "Download Receipt", "Resend WhatsApp", "Edit");
        try
        {
            switch (choice)
            {
                case "Download Receipt":
                    var file = await _client.ReceiptAsync(contribution.Id ?? 0);
                    var path = Path.Combine(FileSystem.CacheDirectory, $"receipt-{contribution.ReceiptNumber}.pdf");
                    using (file) { await using var output = File.Create(path); await file.Stream.CopyToAsync(output); }
                    await Share.Default.RequestAsync(new ShareFileRequest { Title = contribution.ReceiptNumber, File = new ShareFile(path) });
                    break;
                case "Resend WhatsApp":
                    var number = await Shell.Current.DisplayPromptAsync("Resend to WhatsApp", "Mobile number", "Send", "Cancel", initialValue: contribution.WhatsAppNumber, keyboard: Keyboard.Numeric);
                    if (number is null) return;
                    await _client.ResendWhatsappAsync(contribution.Id ?? 0, new ResendWhatsAppRequest { WhatsAppNumber = string.IsNullOrWhiteSpace(number) ? null : number });
                    await Shell.Current.DisplayAlert("Resend WhatsApp", "Receipt resent.", "OK");
                    break;
                case "Edit":
                    await Shell.Current.GoToAsync(nameof(ContributionFormPage),
                        new Dictionary<string, object> { ["festivalId"] = FestivalId, ["contribution"] = contribution });
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't complete that action ({ex.Message}).";
        }
    }
}
