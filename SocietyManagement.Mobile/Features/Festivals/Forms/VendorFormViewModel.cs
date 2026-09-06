using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Features.Festivals.Forms;

public record VendorCategoryOption(string Label, VendorCategory Value);

/// <summary>Mirrors festival-vendors-tab.component.ts's Add/Edit dialog —
/// vendors are society-scoped (reused across every festival), hence
/// SocietyId rather than FestivalId here.</summary>
public partial class VendorFormViewModel : ObservableObject
{
    private readonly FestivalVendorsClient _client;

    public VendorFormViewModel(FestivalVendorsClient client)
    {
        _client = client;
        selectedCategory = CategoryOptions[0];
    }

    public List<VendorCategoryOption> CategoryOptions { get; } = new()
    {
        new("Decorator", VendorCategory.Decorator), new("Sound", VendorCategory.Sound),
        new("Catering", VendorCategory.Catering), new("Electrician", VendorCategory.Electrician),
        new("Tent House", VendorCategory.TentHouse), new("Generator", VendorCategory.Generator),
        new("Photographer", VendorCategory.Photographer), new("Other", VendorCategory.Other),
    };

    [ObservableProperty] private int societyId;
    [ObservableProperty] private int id;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private VendorCategoryOption selectedCategory;
    [ObservableProperty] private string? phone;
    [ObservableProperty] private string? email;
    [ObservableProperty] private string? gstNumber;
    [ObservableProperty] private string? address;
    [ObservableProperty] private decimal rating;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public bool IsEditMode => Id > 0;

    public void LoadFrom(FestivalVendorDto vendor)
    {
        Id = vendor.Id ?? 0;
        Name = vendor.Name ?? string.Empty;
        SelectedCategory = CategoryOptions.FirstOrDefault(o => o.Value == vendor.Category) ?? CategoryOptions[0];
        Phone = vendor.Phone;
        Email = vendor.Email;
        GstNumber = vendor.GstNumber;
        Address = vendor.Address;
        Rating = (decimal)(vendor.Rating ?? 0);
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (IsEditMode)
            {
                await _client.FestivalVendorsPUTAsync(Id, new UpdateVendorCommand
                {
                    Id = Id, Name = Name, Category = SelectedCategory.Value, Phone = Phone, Email = Email,
                    GstNumber = GstNumber, Address = Address, Rating = (double)Rating
                });
            }
            else
            {
                await _client.FestivalVendorsPOSTAsync(new CreateVendorCommand
                {
                    SocietyId = SocietyId, Name = Name, Category = SelectedCategory.Value, Phone = Phone, Email = Email,
                    GstNumber = GstNumber, Address = Address, Rating = (double)Rating
                });
            }

            if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't save the vendor ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
