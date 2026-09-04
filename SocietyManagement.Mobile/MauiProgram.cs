using Microsoft.Extensions.Logging;
using SocietyManagement.Mobile.Core;
using SocietyManagement.Mobile.Core.Auth;
using SocietyManagement.Mobile.Features.Auth;
using SocietyManagement.Mobile.Features.Dashboard;
using SocietyManagement.Mobile.Features.Festivals;
using SocietyManagement.Mobile.Features.Maintenance;
using SocietyManagement.Mobile.Features.ParkingFines;
using SocietyManagement.Mobile.Features.Residents;
using SocietyManagement.Mobile.Features.VehicleSecurity;
using SocietyManagement.Mobile.Features.Visitors;

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

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
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
		builder.Services.AddTransient<MaintenanceDashboardViewModel>();
		builder.Services.AddTransient<MaintenanceDashboardPage>();
		builder.Services.AddTransient<FestivalsListViewModel>();
		builder.Services.AddTransient<FestivalsListPage>();

		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
