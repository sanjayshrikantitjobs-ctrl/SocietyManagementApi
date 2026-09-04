using AspNetCoreRateLimit;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using QuestPDF.Infrastructure;
using Serilog;
using SocietyManagement.API.Extensions;
using SocietyManagement.API.Middleware;
using SocietyManagement.Application;
using SocietyManagement.Infrastructure;
using SocietyManagement.Infrastructure.Hubs;

QuestPDF.Settings.License = LicenseType.Community; // free for this project's scale — see QuestPDF licensing terms

var builder = WebApplication.CreateBuilder(args);

// LocalFileStorageService writes uploads under AppContext.BaseDirectory/wwwroot
// (the build/publish output dir), which is NOT the same as env.WebRootPath —
// that defaults to ContentRootPath/wwwroot, the *project source* dir under
// `dotnet run`. Serving from WebRootPath meant every uploaded photo 404'd
// regardless of when the folder was created, since the two paths never
// pointed at the same place. Must exist before Build() so the file provider
// created below has somewhere real to bind to.
var uploadsRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
Directory.CreateDirectory(Path.Combine(uploadsRoot, "uploads"));

// ---- Serilog (spec: "Serilog Logging") --------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---- Services -----------------------------------------------------------------
// SocietyScopeFilter: global multi-tenant enforcement — see its own doc
// comment. Stateless (reads only from the request's ClaimsPrincipal), so a
// plain instance is fine; no DI registration needed.
// SubscriptionActiveFilter: global subscription-gating enforcement — needs
// IApplicationDbContext/IMemoryCache, so it's added generically and resolved
// via DI per-request instead of being instantiated directly.
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new SocietyManagement.API.Authorization.SocietyScopeFilter());
    options.Filters.Add<SocietyManagement.API.Authorization.SubscriptionActiveFilter>();
});
builder.Services.AddSingleton<SocietyManagement.Application.Common.Interfaces.ISubscriptionCacheInvalidator,
    SocietyManagement.API.Authorization.SubscriptionCacheInvalidator>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:4200", "https://societymanagement-web.sanjay-shrikant-it-jobs.workers.dev" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()); // required for SignalR
});

var app = builder.Build();

// ---- Middleware pipeline --------------------------------------------------------
app.UseMiddleware<GlobalExceptionMiddleware>(); // outermost: catches everything below
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Society Management API v1");
        options.DisplayRequestDuration();
    });
}
else
{
    app.UseHsts();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Society Management API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
// Explicit provider rooted at the same AppContext.BaseDirectory/wwwroot that
// LocalFileStorageService writes to — see uploadsRoot comment above; plain
// UseStaticFiles() follows env.WebRootPath instead, which points elsewhere.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot)
});
app.UseCors("AngularClient");

app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
    .AllowAnonymous()
    .WithTags("Health"); // minimal APIs default to the assembly name as their Swagger tag
                          // otherwise ("SocietyManagement.API") — invalid as a generated C#
                          // identifier, which breaks NSwag client codegen for the Mobile app.

// ---- Database migration + seed -------------------------------------------------
// Requires EF Core migrations to already exist (this sandbox could not run
// `dotnet ef migrations add InitialCreate` — see Database/README.md). Gate with
// "SeedDatabase" so a production deploy can disable it and apply migrations
// through a separate release pipeline step instead.
if (app.Configuration.GetValue("SeedDatabase", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<SocietyManagement.Infrastructure.Persistence.DbSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>().LogError(ex,
            "Database migration/seed failed. If no migrations exist yet, run: " +
            "dotnet ef migrations add InitialCreate -p SocietyManagement.Infrastructure -s SocietyManagement.API, " +
            "then dotnet ef database update.");
    }
}

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
