using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Support;

public record SupportTicketStatusOption(string Label, SupportTicketStatus? Value);

/// <summary>Super Admin only — every bug/support ticket raised by any
/// society's Admin/Member. Read-only for this pass; changing status is a
/// follow-up.</summary>
public partial class SupportTicketsViewModel : ObservableObject
{
    private static readonly SupportTicketStatusOption[] StatusOptionsSeed =
    {
        new("All Statuses", null),
        new("Open", Api.Generated.SupportTicketStatus.Open),
        new("In Progress", Api.Generated.SupportTicketStatus.InProgress),
        new("Resolved", Api.Generated.SupportTicketStatus.Resolved),
    };

    private readonly SupportTicketsClient _client;

    public SupportTicketsViewModel(SupportTicketsClient client) => _client = client;

    public ObservableCollection<SupportTicketStatusOption> StatusOptions { get; } = new(StatusOptionsSeed);

    [ObservableProperty] private ObservableCollection<SupportTicketDto> tickets = new();
    [ObservableProperty] private SupportTicketStatusOption selectedStatus = StatusOptionsSeed[0];
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    partial void OnSelectedStatusChanged(SupportTicketStatusOption value) => _ = LoadCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _client.SupportTicketsGETAsync(SelectedStatus.Value, 1, 100);
            Tickets = new ObservableCollection<SupportTicketDto>(response.Data?.Items ?? new());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load support tickets ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
