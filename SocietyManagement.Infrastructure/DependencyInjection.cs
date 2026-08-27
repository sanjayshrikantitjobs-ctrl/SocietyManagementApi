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
        // Vehicle plate OCR: AsposeOcrVehicleOcrService, replacing every earlier
        // attempt this session (Tesseract — inadequate even correctly cropped;
        // a from-scratch PaddleOCR pipeline — accurate, but its native libraries
        // alone added ~540MB to the Linux deployment package and broke the Azure
        // App Service deploy; Azure AI Vision — accurate but a cloud dependency
        // the user chose to avoid). Aspose.OCR's purpose-built car-plate mode,
        // confirmed live against the real gate photo used throughout this
        // session, reads it far more accurately than anything tried before, and
        // its only heavy dependency (Microsoft.ML.OnnxRuntime) doesn't
        // reintroduce PaddleOCR's deployment-size problem. See
        // AsposeOcrVehicleOcrService.cs.
        //
        // Aspose.OCR is a COMMERCIAL product (perpetual licenses from $799,
        // metered from $1,999/month) — currently running unlicensed/trial,
        // which AsposeOcrVehicleOcrService already strips the watermark text
        // from. Once a license is purchased, set Aspose:LicenseFilePath to the
        // .lic file's path — no code change needed, same "swap in once
        // configured" pattern as Blob Storage/WhatsApp below.
        var asposeLicensePath = configuration["Aspose:LicenseFilePath"];
        if (!string.IsNullOrWhiteSpace(asposeLicensePath) && File.Exists(asposeLicensePath))
        {
            new Aspose.OCR.License().SetLicense(asposeLicensePath);
        }
        services.AddScoped<IVehicleOcrService, AsposeOcrVehicleOcrService>();

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
}
