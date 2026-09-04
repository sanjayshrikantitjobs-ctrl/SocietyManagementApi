using Microsoft.Extensions.DependencyInjection;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Core;

/// <summary>DI registration for every NSwag-generated per-controller client
/// (Api/Generated/ApiClient.g.cs) — one typed HttpClient registration each,
/// all sharing the same base-address configuration and the same
/// AuthDelegatingHandler (bearer-token attachment, single-flight
/// refresh-and-retry — see AddHttpClientWithAuth below), so every generated
/// client behaves identically without repeating that wiring 58 times.</summary>
public static class ApiClientsRegistration
{
    /// <summary>Name of the bare (no auth handler) client AuthDelegatingHandler
    /// itself uses to call refresh-token — see AuthDelegatingHandler.cs.</summary>
    public const string RawClientName = "SocietyApiRaw";

    /// <summary>.NET's default cross-platform managed HTTP stack
    /// (SocketsHttpHandler) resolves DNS differently than native Android
    /// apps do, and is known to fail with "hostname nor servname provided,
    /// or not known" specifically on Android emulators even when the device
    /// otherwise has working internet (e.g. the same URL loads fine in
    /// Chrome). The documented fix is routing HttpClient through Android's
    /// own native HTTP stack instead.</summary>
    private static HttpMessageHandler CreatePlatformHandler()
    {
#if ANDROID
        return new Xamarin.Android.Net.AndroidMessageHandler();
#else
        return new HttpClientHandler();
#endif
    }

    /// <summary>AddHttpClient&lt;T&gt; + shared BaseAddress + platform handler +
    /// AuthDelegatingHandler, in one call — every registration below goes
    /// through this.</summary>
    private static IHttpClientBuilder AddHttpClientWithAuth<T>(this IServiceCollection services) where T : class =>
        services.AddHttpClient<T>(client => client.BaseAddress = new Uri(ApiConfig.ApiBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(CreatePlatformHandler)
            .AddHttpMessageHandler<AuthDelegatingHandler>();

    public static IServiceCollection AddSocietyApiClients(this IServiceCollection services)
    {
        services.AddTransient<AuthDelegatingHandler>();

        // No AuthDelegatingHandler on this one — AuthDelegatingHandler uses it
        // for the refresh-token call itself, so it must never route back
        // through the handler that depends on it (see AuthDelegatingHandler.cs).
        services.AddHttpClient(RawClientName, client => client.BaseAddress = new Uri(ApiConfig.ApiBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(CreatePlatformHandler);

        services.AddHttpClientWithAuth<AuthClient>();
        services.AddHttpClientWithAuth<BuildingsClient>();
        services.AddHttpClientWithAuth<CommitteeClient>();
        services.AddHttpClientWithAuth<ComplaintsClient>();
        services.AddHttpClientWithAuth<DashboardClient>();
        services.AddHttpClientWithAuth<EmergencyContactsClient>();
        services.AddHttpClientWithAuth<EventRsvpsClient>();
        services.AddHttpClientWithAuth<EventsClient>();
        services.AddHttpClientWithAuth<FestivalBudgetCategoriesClient>();
        services.AddHttpClientWithAuth<FestivalContributionsClient>();
        services.AddHttpClientWithAuth<FestivalDashboardClient>();
        services.AddHttpClientWithAuth<FestivalExpensesClient>();
        services.AddHttpClientWithAuth<FestivalSponsorsClient>();
        services.AddHttpClientWithAuth<FestivalTasksClient>();
        services.AddHttpClientWithAuth<FestivalVendorsClient>();
        services.AddHttpClientWithAuth<FestivalVolunteersClient>();
        services.AddHttpClientWithAuth<FestivalsClient>();
        services.AddHttpClientWithAuth<FilesClient>();
        services.AddHttpClientWithAuth<FinanceClient>();
        services.AddHttpClientWithAuth<FineRecordsClient>();
        services.AddHttpClientWithAuth<FlatOccupanciesClient>();
        services.AddHttpClientWithAuth<FlatResaleListingsClient>();
        services.AddHttpClientWithAuth<FlatResidenciesClient>();
        services.AddHttpClientWithAuth<FlatsClient>();
        services.AddHttpClientWithAuth<FloorsClient>();
        services.AddHttpClientWithAuth<GatesClient>();
        services.AddHttpClientWithAuth<HealthClient>();
        services.AddHttpClientWithAuth<MaintenanceBillsClient>();
        services.AddHttpClientWithAuth<MaintenanceCategoriesClient>();
        services.AddHttpClientWithAuth<MaintenanceDashboardClient>();
        services.AddHttpClientWithAuth<MaintenanceSettingsClient>();
        services.AddHttpClientWithAuth<MembersClient>();
        services.AddHttpClientWithAuth<OccupancySettingsClient>();
        services.AddHttpClientWithAuth<ParkingFinesClient>();
        services.AddHttpClientWithAuth<ParkingSlotsClient>();
        services.AddHttpClientWithAuth<PermissionsClient>();
        services.AddHttpClientWithAuth<PersonsClient>();
        services.AddHttpClientWithAuth<PrivacyPolicyClient>();
        services.AddHttpClientWithAuth<RentalAgreementsClient>();
        services.AddHttpClientWithAuth<ResidentDocumentsClient>();
        services.AddHttpClientWithAuth<ResidentsOverviewClient>();
        services.AddHttpClientWithAuth<RolesClient>();
        services.AddHttpClientWithAuth<ServicesClient>();
        services.AddHttpClientWithAuth<SocietiesClient>();
        services.AddHttpClientWithAuth<SpecialChargesClient>();
        services.AddHttpClientWithAuth<StaffClient>();
        services.AddHttpClientWithAuth<SupportTicketsClient>();
        services.AddHttpClientWithAuth<UsersClient>();
        services.AddHttpClientWithAuth<VehicleScansClient>();
        services.AddHttpClientWithAuth<VehiclesClient>();
        services.AddHttpClientWithAuth<VisitorPurposesClient>();
        services.AddHttpClientWithAuth<VisitorSettingsClient>();
        services.AddHttpClientWithAuth<VisitorVisitsClient>();
        services.AddHttpClientWithAuth<VisitorsClient>();
        services.AddHttpClientWithAuth<WaterTankerClient>();
        services.AddHttpClientWithAuth<WaterTankerLogsClient>();
        services.AddHttpClientWithAuth<WingsClient>();
        // VisitorApprovalPublicClient / WhatsAppWebhookClient intentionally excluded —
        // server-rendered/webhook-only endpoints, never called from the mobile app
        // (see the API-inventory research notes behind the approved mobile plan).

        return services;
    }
}
