using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Common;
using SocietyManagement.Infrastructure.Identity;
using SocietyManagement.Infrastructure.Persistence;
using SocietyManagement.Infrastructure.Persistence.Interceptors;
using SocietyManagement.Infrastructure.Persistence.Repositories;
using SocietyManagement.Infrastructure.Services;

namespace SocietyManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

            options.AddInterceptors(
                provider.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                provider.GetRequiredService<DispatchDomainEventsInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IAuditService, AuditService>();

        // Stub-now/swap-later communication providers — see Services/CommunicationServices.cs.
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<ISmsService, StubSmsService>();

        // WhatsAppBusinessApiService (real Meta Graph API calls) kicks in once both
        // WhatsApp:AccessToken and WhatsApp:PhoneNumberId are configured — falls back
        // to the logging-only stub otherwise. See Services/WhatsAppBusinessApiService.cs.
        if (!string.IsNullOrWhiteSpace(configuration["WhatsApp:AccessToken"]) &&
            !string.IsNullOrWhiteSpace(configuration["WhatsApp:PhoneNumberId"]))
        {
            services.AddHttpClient<IWhatsAppService, WhatsAppBusinessApiService>();
        }
        else
        {
            services.AddScoped<IWhatsAppService, StubWhatsAppService>();
        }
        // Vehicle plate OCR provider, in priority order:
        //   1. AzureVisionOcrService, once VisionOcr:Endpoint/ApiKey are configured —
        //      opt-in cloud OCR, not the default (the user explicitly chose to stay
        //      free/local — kept here only in case that changes later).
        //   2. PaddleOcrVehicleOcrService, if its native model/runtime actually loads —
        //      the real default. Chosen after Tesseract (below) proved architecturally
        //      unable to read real plate photos even with a custom plate-region locator
        //      in front of it (a document-OCR engine, not a scene-text one) — confirmed
        //      live against a real gate photo across multiple preprocessing variants and
        //      both official trained-data releases. PaddleOCR's models are trained on
        //      diverse real-world scene text and do their own detection internally.
        //      Constructed eagerly here (not gated on a file-existence check like
        //      Tesseract/tessdata) because its models ship inside the NuGet package —
        //      there's no file to check for. Its failure mode is a native-library load
        //      failure, so construction is wrapped in try/catch: a problem surfaces once,
        //      clearly, in the startup log, instead of as a 500 on the first scan.
        //   3. TesseractVehicleOcrService, once its trained-data folder is present next
        //      to the app — Windows-dev-only in practice: the `Tesseract` NuGet package
        //      (5.2.0) ships no Linux native binaries, so on the real Azure App Service
        //      Linux deployment this branch would throw if ever reached. Kept only as a
        //      harmless local-dev fallback, not a real Linux safety net.
        //   4. StubVehicleOcrService — logging-only, always a low-confidence empty read.
        // Not named "AzureVisionOcr..."/"PaddleOcr..." with an "Azure" prefix in config —
        // see the BlobStorage comment below on why that gets rejected on Azure App Service.
        if (!string.IsNullOrWhiteSpace(configuration["VisionOcr:Endpoint"]) &&
            !string.IsNullOrWhiteSpace(configuration["VisionOcr:ApiKey"]))
        {
            services.AddHttpClient<IVehicleOcrService, AzureVisionOcrService>();
        }
        else
        {
            var paddleOcr = TryCreatePaddleOcrService();
            if (paddleOcr is not null)
            {
                services.AddSingleton<IVehicleOcrService>(paddleOcr);
            }
            else
            {
                var tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (Directory.Exists(tessDataPath) && File.Exists(Path.Combine(tessDataPath, "eng.traineddata")))
                {
                    services.AddScoped<IVehicleOcrService>(provider =>
                        new TesseractVehicleOcrService(tessDataPath, provider.GetRequiredService<ILogger<TesseractVehicleOcrService>>()));
                }
                else
                {
                    services.AddScoped<IVehicleOcrService, StubVehicleOcrService>();
                }
            }
        }

        services.AddSignalR();
        services.AddScoped<INotificationService, NotificationService>();

        // Azure Blob Storage kicks in automatically once BlobStorage:ConnectionString
        // is set (see appsettings.json) — falls back to local disk otherwise, so
        // environments that haven't configured it yet keep working unchanged. Deliberately
        // not named "AzureBlobStorage..." — Azure App Service's Environment variables blade
        // rejects app setting names starting with "Azure" as reserved for its own
        // platform-managed connections (confirmed live: "AppSetting with name
        // 'AzureBlobStorage__ConnectionString' is not allowed").
        var blobConnectionString = configuration["BlobStorage:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(blobConnectionString))
        {
            var containerName = configuration["BlobStorage:ContainerName"] ?? "uploads";
            services.AddSingleton(new BlobServiceClient(blobConnectionString));
            services.AddScoped<IFileStorageService>(provider =>
                new AzureBlobFileStorageService(provider.GetRequiredService<BlobServiceClient>(), containerName));
        }
        else
        {
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
        }
        services.AddScoped<IPdfReceiptService, PdfReceiptService>();
        services.AddScoped<IMaintenanceBillPdfService, MaintenanceBillPdfService>();
        services.AddScoped<IResidentImportService, ClosedXmlResidentImportService>();
        services.AddScoped<IFinanceReportService, FinanceReportService>();
        services.AddHostedService<MaintenanceBillGenerationService>();
        services.AddHostedService<VisitorRequestExpiryService>();

        services.AddScoped<DbSeeder>();

        return services;
    }

    /// <summary>Constructs PaddleOcrVehicleOcrService eagerly at startup, inside
    /// a try/catch — its native model/inference library can fail to load
    /// (DllNotFoundException, BadImageFormatException, an unsupported CPU
    /// instruction set) in a way no config value or file-existence check can
    /// predict in advance. Returns null on failure so the caller falls through
    /// to Tesseract/Stub instead of crashing the whole app at startup; the
    /// failure itself is still logged once, clearly, here.</summary>
    private static PaddleOcrVehicleOcrService? TryCreatePaddleOcrService()
    {
        // Deliberately not disposed: the logger handed to a successfully
        // constructed singleton service must keep working for the app's whole
        // lifetime, not just this one startup call.
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<PaddleOcrVehicleOcrService>();

        try
        {
            return new PaddleOcrVehicleOcrService(logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vehicle OCR: PaddleOCR engine failed to initialize; falling back to the next provider.");
            loggerFactory.Dispose();
            return null;
        }
    }
}
