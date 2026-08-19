using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Maintenance;

public class MaintenanceSettingsDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public int BillGenerationDay { get; set; }
    public int DueDay { get; set; }
    public int GracePeriodDays { get; set; }
    public decimal LateFeeAmount { get; set; }
    public string InvoiceNumberPrefix { get; set; } = default!;
    public int NextInvoiceNumber { get; set; }
    public string WhatsAppMessageTemplate { get; set; } = default!;
    public string PdfFooterMessage { get; set; } = default!;
}

// ---- Commands ----------------------------------------------------------------
/// <summary>Single upsert — there's exactly one settings row per society, so
/// there's no separate create/update distinction from the caller's perspective.</summary>
public record UpsertMaintenanceSettingsCommand(
    int SocietyId, int BillGenerationDay, int DueDay, int GracePeriodDays, decimal LateFeeAmount,
    string InvoiceNumberPrefix, string WhatsAppMessageTemplate, string PdfFooterMessage) : IRequest<Unit>;

public class UpsertMaintenanceSettingsCommandValidator : AbstractValidator<UpsertMaintenanceSettingsCommand>
{
    public UpsertMaintenanceSettingsCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.BillGenerationDay).InclusiveBetween(1, 28);
        RuleFor(x => x.DueDay).InclusiveBetween(1, 28);
        RuleFor(x => x.GracePeriodDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LateFeeAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InvoiceNumberPrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.WhatsAppMessageTemplate).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.PdfFooterMessage).NotEmpty().MaximumLength(500);
    }
}

public class MaintenanceSettingsCommandHandlers : IRequestHandler<UpsertMaintenanceSettingsCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public MaintenanceSettingsCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(UpsertMaintenanceSettingsCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var settings = await _context.MaintenanceSettings
            .FirstOrDefaultAsync(s => s.SocietyId == request.SocietyId && !s.IsDeleted, ct);

        if (settings is null)
        {
            settings = new MaintenanceSettings { SocietyId = request.SocietyId };
            await _context.MaintenanceSettings.AddAsync(settings, ct);
        }

        settings.BillGenerationDay = request.BillGenerationDay;
        settings.DueDay = request.DueDay;
        settings.GracePeriodDays = request.GracePeriodDays;
        settings.LateFeeAmount = request.LateFeeAmount;
        settings.InvoiceNumberPrefix = request.InvoiceNumberPrefix;
        settings.WhatsAppMessageTemplate = request.WhatsAppMessageTemplate;
        settings.PdfFooterMessage = request.PdfFooterMessage;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Maintenance", nameof(MaintenanceSettings), settings.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
/// <summary>Get-or-create — the settings screen should never show a null
/// state; a fresh society gets the entity's default values on first read.</summary>
public record GetMaintenanceSettingsQuery(int SocietyId) : IRequest<MaintenanceSettingsDto>;

public class MaintenanceSettingsQueryHandlers : IRequestHandler<GetMaintenanceSettingsQuery, MaintenanceSettingsDto>
{
    private readonly IApplicationDbContext _context;

    public MaintenanceSettingsQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<MaintenanceSettingsDto> Handle(GetMaintenanceSettingsQuery request, CancellationToken ct)
    {
        var settings = await _context.MaintenanceSettings
            .FirstOrDefaultAsync(s => s.SocietyId == request.SocietyId && !s.IsDeleted, ct);

        if (settings is null)
        {
            if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
            {
                throw new NotFoundException(nameof(Society), request.SocietyId);
            }

            settings = new MaintenanceSettings { SocietyId = request.SocietyId };
            await _context.MaintenanceSettings.AddAsync(settings, ct);
            await _context.SaveChangesAsync(ct);
        }

        return new MaintenanceSettingsDto
        {
            Id = settings.Id, SocietyId = settings.SocietyId, BillGenerationDay = settings.BillGenerationDay,
            DueDay = settings.DueDay, GracePeriodDays = settings.GracePeriodDays, LateFeeAmount = settings.LateFeeAmount,
            InvoiceNumberPrefix = settings.InvoiceNumberPrefix, NextInvoiceNumber = settings.NextInvoiceNumber,
            WhatsAppMessageTemplate = settings.WhatsAppMessageTemplate, PdfFooterMessage = settings.PdfFooterMessage
        };
    }
}
