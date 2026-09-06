using Microsoft.Extensions.Logging;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Auth;
using SocietyManagement.Mobile.Features.Dashboard;
using SocietyManagement.Mobile.Features.Festivals;
using SocietyManagement.Mobile.Features.Festivals.Forms;
using SocietyManagement.Mobile.Features.Maintenance;
using SocietyManagement.Mobile.Features.Maintenance.Forms;
using SocietyManagement.Mobile.Features.Maintenance.Payments;
using SocietyManagement.Mobile.Features.ParkingFines;
using SocietyManagement.Mobile.Features.Residents;
using SocietyManagement.Mobile.Features.VehicleSecurity;
using SocietyManagement.Mobile.Features.Visitors;
using SocietyManagement.Mobile.Features.Visitors.Forms;
using SocietyManagement.Mobile.Shared;

namespace SocietyManagement.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<ITokenStorage, SecureStorageTokenStorage>();
		builder.Services.AddSingleton<AuthState>();
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<CurrentSocietyService>();
		builder.Services.AddSocietyApiClients();

		builder.Services.AddTransient<TopBarView>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<ChangePasswordViewModel>();
		builder.Services.AddTransient<ChangePasswordPage>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<DashboardPage>();

		builder.Services.AddTransient<NewVisitorViewModel>();
		builder.Services.AddTransient<NewVisitorPage>();
		builder.Services.AddTransient<CurrentlyInsideViewModel>();
		builder.Services.AddTransient<CurrentlyInsidePage>();

		builder.Services.AddTransient<VehicleScanViewModel>();
		builder.Services.AddTransient<VehicleScanPage>();
		builder.Services.AddTransient<VehicleScanHistoryViewModel>();
		builder.Services.AddTransient<VehicleScanHistoryPage>();

		builder.Services.AddTransient<ParkingFinesViewModel>();
		builder.Services.AddTransient<ParkingFinesPage>();

		builder.Services.AddTransient<ResidentsListViewModel>();
		builder.Services.AddTransient<ResidentsListPage>();
		builder.Services.AddTransient<MaintenanceViewModel>();
		builder.Services.AddTransient<MaintenancePage>();
		builder.Services.AddTransient<MaintenanceBillDetailViewModel>();
		builder.Services.AddTransient<MaintenanceBillDetailPage>();
		builder.Services.AddTransient<RecordPaymentViewModel>();
		builder.Services.AddTransient<RecordPaymentPage>();
		builder.Services.AddTransient<FestivalsListViewModel>();
		builder.Services.AddTransient<FestivalsListPage>();
		builder.Services.AddTransient<FestivalDetailViewModel>();
		builder.Services.AddTransient<FestivalDetailPage>();
		builder.Services.AddTransient<BudgetRevisionsViewModel>();
		builder.Services.AddTransient<BudgetRevisionsPage>();
		builder.Services.AddTransient<FlatContributionDetailViewModel>();
		builder.Services.AddTransient<FlatContributionDetailPage>();

		builder.Services.AddTransient<BudgetCategoryFormViewModel>();
		builder.Services.AddTransient<BudgetCategoryFormPage>();
		builder.Services.AddTransient<SponsorFormViewModel>();
		builder.Services.AddTransient<SponsorFormPage>();
		builder.Services.AddTransient<VendorFormViewModel>();
		builder.Services.AddTransient<VendorFormPage>();
		builder.Services.AddTransient<VolunteerFormViewModel>();
		builder.Services.AddTransient<VolunteerFormPage>();
		builder.Services.AddTransient<TaskFormViewModel>();
		builder.Services.AddTransient<TaskFormPage>();
		builder.Services.AddTransient<ExpenseFormViewModel>();
		builder.Services.AddTransient<ExpenseFormPage>();
		builder.Services.AddTransient<ContributionFormViewModel>();
		builder.Services.AddTransient<ContributionFormPage>();

		builder.Services.AddTransient<SpecialChargeFormViewModel>();
		builder.Services.AddTransient<SpecialChargeFormPage>();
		builder.Services.AddTransient<FineFormViewModel>();
		builder.Services.AddTransient<FineFormPage>();
		builder.Services.AddTransient<WaterTankerLogFormViewModel>();
		builder.Services.AddTransient<WaterTankerLogFormPage>();

		builder.Services.AddTransient<GatesViewModel>();
		builder.Services.AddTransient<GatesPage>();
		builder.Services.AddTransient<GateFormViewModel>();
		builder.Services.AddTransient<GateFormPage>();
		builder.Services.AddTransient<PurposesViewModel>();
		builder.Services.AddTransient<PurposesPage>();
		builder.Services.AddTransient<PurposeFormViewModel>();
		builder.Services.AddTransient<PurposeFormPage>();
		builder.Services.AddTransient<VisitorSettingsViewModel>();
		builder.Services.AddTransient<VisitorSettingsPage>();

		builder.Services.AddTransient<ComingSoonPage>();
		builder.Services.AddTransient<ContactUsPage>();

		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
