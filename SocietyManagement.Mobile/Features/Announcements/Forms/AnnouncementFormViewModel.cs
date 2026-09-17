using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Announcements.Forms;

public record AnnouncementTypeOption(string Label, AnnouncementType Value);
public record AnnouncementPriorityOption(string Label, AnnouncementPriority Value);

/// <summary>Mirrors announcement-form-dialog.component.ts: same fields
/// (Title, Description, Type, Priority, optional scheduled Publish/Expiry,
/// optional attachment, PublishNow on create). PublishAt/ExpiryAt are sent
/// as a DateTimeOffset with a zero UTC offset built from the picked local
/// wall-clock values — deliberately mirroring the web form's behavior, which
/// sends an offset-less local datetime string that the API stores verbatim
/// (see AnnouncementLifecycleService, which compares against DateTime.UtcNow
/// directly); a real local-to-UTC conversion here would silently diverge
/// from what the web app already does for the same input.</summary>
public partial class AnnouncementFormViewModel : ObservableObject
{
    private readonly AnnouncementsClient _client;
    private readonly FilesClient _filesClient;

    public AnnouncementFormViewModel(AnnouncementsClient client, FilesClient filesClient)
    {
        _client = client;
        _filesClient = filesClient;

        TypeOptions = new ObservableCollection<AnnouncementTypeOption>
        {
            new("General", AnnouncementType.General), new("Society Meeting", AnnouncementType.Meeting),
            new("Festival", AnnouncementType.Festival), new("Maintenance", AnnouncementType.Maintenance),
            new("Water/Electricity Notice", AnnouncementType.UtilityNotice), new("Lost & Found", AnnouncementType.LostAndFound),
            new("Emergency", AnnouncementType.Emergency), new("Event", AnnouncementType.Event),
            new("Parking", AnnouncementType.Parking), new("Security", AnnouncementType.Security),
            new("Important Notice", AnnouncementType.Important), new("Other", AnnouncementType.Other)
        };
        PriorityOptions = new ObservableCollection<AnnouncementPriorityOption>
        {
            new("Low", AnnouncementPriority.Low), new("Normal", AnnouncementPriority.Normal),
            new("High", AnnouncementPriority.High), new("Urgent", AnnouncementPriority.Urgent)
        };
        SelectedType = TypeOptions[0];
        SelectedPriority = PriorityOptions[1];
        PublishDate = DateTime.Today;
        PublishTime = TimeSpan.Zero;
        ExpiryDate = DateTime.Today;
        ExpiryTime = TimeSpan.Zero;
    }

    public ObservableCollection<AnnouncementTypeOption> TypeOptions { get; }
    public ObservableCollection<AnnouncementPriorityOption> PriorityOptions { get; }

    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private AnnouncementTypeOption? selectedType;
    [ObservableProperty] private AnnouncementPriorityOption? selectedPriority;

    [ObservableProperty] private bool hasPublishAt;
    [ObservableProperty] private DateTime publishDate;
    [ObservableProperty] private TimeSpan publishTime;

    [ObservableProperty] private bool hasExpiryAt;
    [ObservableProperty] private DateTime expiryDate;
    [ObservableProperty] private TimeSpan expiryTime;

    [ObservableProperty] private string? attachmentUrl;
    [ObservableProperty] private bool isUploadingAttachment;

    [ObservableProperty] private bool publishNow;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    partial void OnPublishDateChanged(DateTime value) => HasPublishAt = true;
    partial void OnPublishTimeChanged(TimeSpan value) => HasPublishAt = true;
    partial void OnExpiryDateChanged(DateTime value) => HasExpiryAt = true;
    partial void OnExpiryTimeChanged(TimeSpan value) => HasExpiryAt = true;

    [RelayCommand] private void ClearPublishAt() => HasPublishAt = false;
    [RelayCommand] private void ClearExpiryAt() => HasExpiryAt = false;

    public void LoadFrom(AnnouncementDto announcement)
    {
        Id = announcement.Id ?? 0;
        Title = announcement.Title ?? string.Empty;
        Description = announcement.Description ?? string.Empty;
        SelectedType = TypeOptions.FirstOrDefault(t => t.Value == announcement.Type) ?? TypeOptions[0];
        SelectedPriority = PriorityOptions.FirstOrDefault(p => p.Value == announcement.Priority) ?? PriorityOptions[1];
        AttachmentUrl = announcement.AttachmentUrl;

        if (announcement.PublishAt is DateTimeOffset publishAt)
        {
            PublishDate = publishAt.Date;
            PublishTime = publishAt.TimeOfDay;
            HasPublishAt = true;
        }
        if (announcement.ExpiryAt is DateTimeOffset expiryAt)
        {
            ExpiryDate = expiryAt.Date;
            ExpiryTime = expiryAt.TimeOfDay;
            HasExpiryAt = true;
        }

        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task PickAttachmentAsync()
    {
        ErrorMessage = null;
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null) return;

            IsUploadingAttachment = true;
            using var stream = await photo.OpenReadAsync();
            var response = await _filesClient.UploadAsync(new FileParameter(stream, photo.FileName, "image/jpeg"), "announcements");
            AttachmentUrl = response.Data;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't attach the image ({ex.Message}).";
        }
        finally
        {
            IsUploadingAttachment = false;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description) || SelectedType is null || SelectedPriority is null)
        {
            ErrorMessage = "Enter a title, description, type and priority.";
            return;
        }

        var publishAt = HasPublishAt ? new DateTimeOffset(PublishDate.Date + PublishTime, TimeSpan.Zero) : (DateTimeOffset?)null;
        var expiryAt = HasExpiryAt ? new DateTimeOffset(ExpiryDate.Date + ExpiryTime, TimeSpan.Zero) : (DateTimeOffset?)null;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.AnnouncementsPUTAsync(Id, new UpdateAnnouncementCommand
                {
                    Id = Id, Title = Title, Description = Description, Type = SelectedType.Value, Priority = SelectedPriority.Value,
                    AttachmentUrl = string.IsNullOrWhiteSpace(AttachmentUrl) ? null : AttachmentUrl, PublishAt = publishAt, ExpiryAt = expiryAt
                });
            }
            else
            {
                await _client.AnnouncementsPOSTAsync(new CreateAnnouncementCommand
                {
                    SocietyId = SocietyId, Title = Title, Description = Description, Type = SelectedType.Value, Priority = SelectedPriority.Value,
                    AttachmentUrl = string.IsNullOrWhiteSpace(AttachmentUrl) ? null : AttachmentUrl, PublishAt = publishAt, ExpiryAt = expiryAt,
                    PublishNow = PublishNow
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the announcement ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
