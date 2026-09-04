namespace SocietyManagement.Mobile.Core;

/// <summary>Single source of truth for where the mobile app's API calls and
/// SignalR connection go — mirrors the role SocietyManagement.Web's
/// environment.ts/environment.prod.ts play for the Angular app. Trailing
/// slash on ApiBaseUrl is required: every NSwag-generated client
/// (Api/Generated/ApiClient.g.cs) builds request URIs as a RELATIVE path
/// (e.g. "api/Auth/login") resolved against HttpClient.BaseAddress — without
/// the trailing slash, standard Uri combination rules would drop the last
/// path segment of the base address.</summary>
public static class ApiConfig
{
    public const string ApiBaseUrl = "https://societymanagement-api-f8cqfjajdkh4dpey.centralindia-01.azurewebsites.net/";

    public const string HubUrl = "https://societymanagement-api-f8cqfjajdkh4dpey.centralindia-01.azurewebsites.net/hubs/notifications";
}
