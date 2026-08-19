using AspNetCoreRateLimit;

namespace SocietyManagement.API.Extensions;

/// <summary>IP-based rate limiting (spec: "Rate Limiting") configured from the
/// "IpRateLimiting" section of appsettings.json — defaults are conservative for
/// auth endpoints and generous for read endpoints; tune per-environment.</summary>
public static class RateLimitServiceExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
        services.AddInMemoryRateLimiting();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        return services;
    }
}
