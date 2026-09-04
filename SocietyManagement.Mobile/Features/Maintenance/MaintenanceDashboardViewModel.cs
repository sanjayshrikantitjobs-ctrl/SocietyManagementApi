using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Features.Maintenance;

/// <summary>Mirrors maintenance-dashboard.component.ts's KPI cards — the
/// same GetMaintenanceDashboardQuery this session's whole billing-fix pass
/// hardened (cumulative Total/Balance, Overdue/Pending/PartiallyPaid/Paid
/// status rules, month-scoped Outstanding). Month/Year selection and the
/// charts are a follow-up; this is the KPI-card slice.</summary>
public partial class MaintenanceDashboardViewModel : ObservableObject
{
    private readonly MaintenanceDashboardClient _dashboardClient;
    private readonly CurrentSocietyService _currentSocietyService;

    public MaintenanceDashboardViewModel(MaintenanceDashboardClient dashboardClient, CurrentSocietyService currentSocietyService)
    {
        _dashboardClient = dashboardClient;
        _currentSocietyService = currentSocietyService;
    }

    [ObservableProperty] private MaintenanceKpisDto? kpis;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var societyId = await _currentSocietyService.GetSocietyIdAsync();
        if (societyId is null)
        {
            ErrorMessage = "No society available for this account.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _dashboardClient.DashboardAsync(societyId, DateTime.Today, null);
            Kpis = response.Data?.Kpis;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load the maintenance dashboard ({ex.Message}).";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
