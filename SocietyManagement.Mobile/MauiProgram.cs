using Microcharts.Maui;
using Microsoft.Extensions.Logging;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Auth;
using SocietyManagement.Mobile.Features.Committee;
using SocietyManagement.Mobile.Features.Complaints;
using SocietyManagement.Mobile.Features.Dashboard;
using SocietyManagement.Mobile.Features.Festivals;
using SocietyManagement.Mobile.Features.Festivals.Forms;
using SocietyManagement.Mobile.Features.Finance;
using SocietyManagement.Mobile.Features.Finance.Forms;
using SocietyManagement.Mobile.Features.Maintenance;
using SocietyManagement.Mobile.Features.Maintenance.Forms;
using SocietyManagement.Mobile.Features.Maintenance.Payments;
using SocietyManagement.Mobile.Features.ParkingFines;
using SocietyManagement.Mobile.Features.Residents;
using SocietyManagement.Mobile.Features.Residents.Forms;
using SocietyManagement.Mobile.Features.Roles;
using SocietyManagement.Mobile.Features.Services;
using SocietyManagement.Mobile.Features.Societies;
using SocietyManagement.Mobile.Features.Staff;
using SocietyManagement.Mobile.Features.Support;
using SocietyManagement.Mobile.Features.Users;
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
			.UseMicrocharts()
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

		builder.Services.AddTransient<ResidentsViewModel>();
		builder.Services.AddTransient<ResidentsPage>();
		builder.Services.AddTransient<MyFamilyViewModel>();
		builder.Services.AddTransient<MyFamilyPage>();
		builder.Services.AddTransient<FlatResidentDetailViewModel>();
		builder.Services.AddTransient<FlatResidentDetailPage>();
		builder.Services.AddTransient<OccupancyMemberFormViewModel>();
		builder.Services.AddTransient<OccupancyMemberFormPage>();
		builder.Services.AddTransient<EmergencyContactFormViewModel>();
		builder.Services.AddTransient<EmergencyContactFormPage>();
		builder.Services.AddTransient<VehicleFormViewModel>();
		builder.Services.AddTransient<VehicleFormPage>();
		builder.Services.AddTransient<ResaleListingFormViewModel>();
		builder.Services.AddTransient<ResaleListingFormPage>();
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

		builder.Services.AddTransient<FinanceViewModel>();
		builder.Services.AddTransient<FinancePage>();
		builder.Services.AddTransient<FinanceExpenseFormViewModel>();
		builder.Services.AddTransient<FinanceExpenseFormPage>();

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

		builder.Services.AddTransient<StaffViewModel>();
		builder.Services.AddTransient<StaffPage>();
		builder.Services.AddTransient<ServicesViewModel>();
		builder.Services.AddTransient<ServicesPage>();
		builder.Services.AddTransient<CommitteeViewModel>();
		builder.Services.AddTransient<CommitteePage>();
		builder.Services.AddTransient<ComplaintsViewModel>();
		builder.Services.AddTransient<ComplaintsPage>();
		builder.Services.AddTransient<MyComplaintsViewModel>();
		builder.Services.AddTransient<MyComplaintsPage>();
		builder.Services.AddTransient<SocietiesViewModel>();
		builder.Services.AddTransient<SocietiesPage>();
		builder.Services.AddTransient<SocietyFormViewModel>();
		builder.Services.AddTransient<SocietyFormPage>();
		builder.Services.AddTransient<SocietyStructureViewModel>();
		builder.Services.AddTransient<SocietyStructurePage>();
		builder.Services.AddTransient<ParkingSlotsViewModel>();
		builder.Services.AddTransient<ParkingSlotsPage>();
		builder.Services.AddTransient<UsersViewModel>();
		builder.Services.AddTransient<UsersPage>();
		builder.Services.AddTransient<RolesViewModel>();
		builder.Services.AddTransient<RolesPage>();
		builder.Services.AddTransient<SupportTicketsViewModel>();
		builder.Services.AddTransient<SupportTicketsPage>();

		builder.Services.AddTransient<ComingSoonPage>();
		builder.Services.AddTransient<ContactUsPage>();

		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
