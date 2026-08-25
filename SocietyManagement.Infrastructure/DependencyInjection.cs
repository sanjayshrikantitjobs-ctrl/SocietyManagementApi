using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<IWhatsAppService, StubWhatsAppService>();

        services.AddSignalR();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();
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
