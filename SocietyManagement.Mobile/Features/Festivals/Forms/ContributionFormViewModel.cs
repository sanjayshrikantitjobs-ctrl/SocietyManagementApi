using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public record ContributionFlatOption(string Label, int? Value);
public record ContributionPaymentModeOption(string Label, ContributionPaymentMethod Value);

/// <summary>Mirrors festival-contribution-tab.component.ts's Record/Edit
/// Contribution dialog. When opened from a specific flat's detail page the
/// flat is locked (no picker, matching the web's flat-detail dialog variant
/// which never shows flatId either); the toolbar's own "Record Contribution"
/// shows the full flat picker built from GetContributableFlats. Editing an
/// existing contribution drops both the flat picker and the WhatsApp field,
/// exactly like UpdateContributionCommand's own narrower shape.</summary>
public partial class ContributionFormViewModel : ObservableObject
{
    private readonly FestivalContributionsClient _client;

    private static readonly ContributionFlatOption GuestOption = new("— Guest / No Flat —", null);

    public ContributionFormViewModel(FestivalContributionsClient client)
    {
        _client = client;
        selectedFlat = GuestOption;
        selectedPaymentMode = PaymentModeOptions[0];
    }

    public List<ContributionPaymentModeOption> PaymentModeOptions { get; } = new()
    {
        new("Cash", ContributionPaymentMethod.Cash), new("UPI", ContributionPaymentMethod.UPI), new("Bank Transfer", ContributionPaymentMethod.BankTransfer),
    };

    [ObservableProperty] private List<ContributionFlatOption> flatOptions = new() { GuestOption };
    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int id;
    [ObservableProperty] private int? lockedFlatId;
    [ObservableProperty] private string? lockedFlatNumber;
    [ObservableProperty] private ContributionFlatOption selectedFlat;
    [ObservableProperty] private string memberName = string.Empty;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private ContributionPaymentModeOption selectedPaymentMode;
    [ObservableProperty] private DateTime paymentDate = DateTime.Today;
    [ObservableProperty] private string? transactionId;
    [ObservableProperty] private string? whatsAppNumber;
    [ObservableProperty] private bool isAnonymous;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;
    public bool ShowFlatPicker => !IsEditMode && LockedFlatId is null;
    public bool ShowLockedFlat => !IsEditMode && LockedFlatId is not null;
    public bool ShowWhatsApp => !IsEditMode;

    /// <summary>Set via ApplyQueryAttributes when opened from a specific
    /// flat's detail page — must re-notify ShowFlatPicker/ShowLockedFlat
    /// (both computed off this) since they were already bound, at their
    /// construction-time values, before this property changed.</summary>
    partial void OnLockedFlatIdChanged(int? value)
    {
        OnPropertyChanged(nameof(ShowFlatPicker));
        OnPropertyChanged(nameof(ShowLockedFlat));
    }

    async partial void OnFestivalIdChanged(int value)
    {
        if (ShowFlatPicker) await LoadFlatsAsync();
    }

    private async Task LoadFlatsAsync()
    {
        try
        {
            var response = await _client.ContributableFlatsAsync(FestivalId);
            var options = new List<ContributionFlatOption> { GuestOption };
            options.AddRange((response.Data ?? new()).Select(f => new ContributionFlatOption(f.FlatNumber ?? "—", f.FlatId)));
            FlatOptions = options;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load flats ({ex.Message}).";
        }
    }

    public void LoadFrom(FestivalContributionDto contribution)
    {
        Id = contribution.Id ?? 0;
        MemberName = contribution.MemberName ?? string.Empty;
        Amount = (decimal)(contribution.Amount ?? 0);
        SelectedPaymentMode = PaymentModeOptions.FirstOrDefault(o => o.Value == contribution.PaymentMethod) ?? PaymentModeOptions[0];
        if (contribution.PaymentDate is DateTimeOffset date) PaymentDate = date.Date;
        TransactionId = contribution.TransactionId;
        IsAnonymous = contribution.IsAnonymous ?? false;
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(ShowFlatPicker));
        OnPropertyChanged(nameof(ShowLockedFlat));
        OnPropertyChanged(nameof(ShowWhatsApp));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(MemberName) || Amount <= 0)
        {
            ErrorMessage = "Enter a donor name and an amount greater than zero.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.FestivalContributionsPUTAsync(Id, new UpdateContributionCommand
                {
                    Id = Id, MemberName = MemberName, Amount = (double)Amount, PaymentMethod = SelectedPaymentMode.Value,
                    PaymentDate = PaymentDate, TransactionId = TransactionId, IsAnonymous = IsAnonymous
                });
            }
            else
            {
                var flatId = LockedFlatId ?? SelectedFlat.Value;
                await _client.FestivalContributionsPOSTAsync(new CreateContributionCommand
                {
                    FestivalId = FestivalId, FlatId = flatId, MemberName = MemberName, Amount = (double)Amount,
                    PaymentMethod = SelectedPaymentMode.Value, PaymentDate = PaymentDate, TransactionId = TransactionId,
                    IsAnonymous = IsAnonymous, WhatsAppNumber = string.IsNullOrWhiteSpace(WhatsAppNumber) ? null : WhatsAppNumber
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the contribution ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
