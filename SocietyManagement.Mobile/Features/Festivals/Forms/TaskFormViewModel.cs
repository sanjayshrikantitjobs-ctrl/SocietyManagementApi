using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public record VolunteerOption(string Label, int? Value);
public record TaskStatusOption(string Label, FestivalTaskStatus Value);

/// <summary>Mirrors festival-tasks-tab.component.ts's Add/Edit dialog — a
/// task can only be assigned to a Volunteer already added on that tab
/// (there's no separate "assign to member" concept), and Status is only
/// editable once a task already exists (new tasks start at the backend's
/// own default).</summary>
public partial class TaskFormViewModel : ObservableObject
{
    private readonly FestivalTasksClient _client;
    private readonly FestivalVolunteersClient _volunteersClient;

    public TaskFormViewModel(FestivalTasksClient client, FestivalVolunteersClient volunteersClient)
    {
        _client = client;
        _volunteersClient = volunteersClient;
        selectedVolunteer = UnassignedOption;
        selectedStatus = StatusOptions[0];
    }

    private static readonly VolunteerOption UnassignedOption = new("Unassigned", null);

    public List<TaskStatusOption> StatusOptions { get; } = new()
    {
        new("Pending", FestivalTaskStatus.Pending), new("In Progress", FestivalTaskStatus.InProgress), new("Completed", FestivalTaskStatus.Completed),
    };

    [ObservableProperty] private List<VolunteerOption> volunteerOptions = new() { UnassignedOption };
    [ObservableProperty] private int festivalId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string? description;
    [ObservableProperty] private VolunteerOption selectedVolunteer;
    [ObservableProperty] private TaskStatusOption selectedStatus;
    [ObservableProperty] private DateTime dueDate = DateTime.Today;
    [ObservableProperty] private bool hasDueDate;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    async partial void OnFestivalIdChanged(int value) => await LoadVolunteersAsync();

    private async Task LoadVolunteersAsync()
    {
        if (FestivalId <= 0) return;
        try
        {
            var response = await _volunteersClient.FestivalVolunteersGETAsync(FestivalId);
            var options = new List<VolunteerOption> { UnassignedOption };
            options.AddRange((response.Data ?? new()).Select(v => new VolunteerOption(v.Name ?? "—", v.Id)));
            VolunteerOptions = options;
            if (_pendingVolunteerId is int pending)
                SelectedVolunteer = VolunteerOptions.FirstOrDefault(o => o.Value == pending) ?? UnassignedOption;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load volunteers ({ex.Message}).";
        }
    }

    private int? _pendingVolunteerId;

    public void LoadFrom(FestivalTaskDto task)
    {
        Id = task.Id ?? 0;
        Title = task.Title ?? string.Empty;
        Description = task.Description;
        SelectedStatus = StatusOptions.FirstOrDefault(o => o.Value == task.Status) ?? StatusOptions[0];
        if (task.DueDate is DateTimeOffset due) { DueDate = due.Date; HasDueDate = true; }
        _pendingVolunteerId = task.AssignedVolunteerId;
        SelectedVolunteer = VolunteerOptions.FirstOrDefault(o => o.Value == task.AssignedVolunteerId) ?? UnassignedOption;
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private void ClearDueDate() => HasDueDate = false;

    partial void OnDueDateChanged(DateTime value) => HasDueDate = true;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Title is required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var dueDate = HasDueDate ? (DateTime?)DueDate : null;
            if (IsEditMode)
            {
                await _client.FestivalTasksPUTAsync(Id, new UpdateTaskCommand
                {
                    Id = Id, Title = Title, Description = Description, AssignedVolunteerId = SelectedVolunteer.Value,
                    Status = SelectedStatus.Value, DueDate = dueDate
                });
            }
            else
            {
                await _client.FestivalTasksPOSTAsync(new CreateTaskCommand
                {
                    FestivalId = FestivalId, Title = Title, Description = Description,
                    AssignedVolunteerId = SelectedVolunteer.Value, DueDate = dueDate
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the task ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
