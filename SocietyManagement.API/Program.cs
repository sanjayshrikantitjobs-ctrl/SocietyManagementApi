using AspNetCoreRateLimit;
using QuestPDF.Infrastructure;
using Serilog;
using SocietyManagement.API.Extensions;
using SocietyManagement.API.Middleware;
using SocietyManagement.Application;
using SocietyManagement.Infrastructure;
using SocietyManagement.Infrastructure.Hubs;

QuestPDF.Settings.License = LicenseType.Community; // free for this project's scale — see QuestPDF licensing terms

var builder = WebApplication.CreateBuilder(args);

// ---- Serilog (spec: "Serilog Logging") --------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---- Services -----------------------------------------------------------------
builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddSwaggerDocumentation();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:4200" };

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

if (app.Environment.IsDevelopment())
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
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // serves wwwroot/uploads/** — see LocalFileStorageService

app.UseIpRateLimiting();

app.UseCors("AngularClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
    .AllowAnonymous();

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
