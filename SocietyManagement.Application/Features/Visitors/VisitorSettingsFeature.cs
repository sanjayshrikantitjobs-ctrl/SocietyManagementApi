using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Visitors;

public class VisitorSettingsDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public int ApprovalRequestExpiryMinutes { get; set; }
    public int RetentionDays { get; set; }
}

// ---- Commands ----------------------------------------------------------------
/// <summary>Single upsert — one settings row per society, mirrors
/// MaintenanceSettingsFeature's shape.</summary>
public record UpsertVisitorSettingsCommand(int SocietyId, int ApprovalRequestExpiryMinutes, int RetentionDays) : IRequest<Unit>;

public class UpsertVisitorSettingsCommandValidator : AbstractValidator<UpsertVisitorSettingsCommand>
{
    public UpsertVisitorSettingsCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.ApprovalRequestExpiryMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.RetentionDays).InclusiveBetween(1, 3650);
    }
}

public class VisitorSettingsCommandHandlers : IRequestHandler<UpsertVisitorSettingsCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public VisitorSettingsCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(UpsertVisitorSettingsCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var settings = await _context.VisitorSettings.FirstOrDefaultAsync(s => s.SocietyId == request.SocietyId && !s.IsDeleted, ct);
        if (settings is null)
        {
            settings = new VisitorSettings { SocietyId = request.SocietyId };
            await _context.VisitorSettings.AddAsync(settings, ct);
        }

        settings.ApprovalRequestExpiryMinutes = request.ApprovalRequestExpiryMinutes;
        settings.RetentionDays = request.RetentionDays;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Visitors", nameof(VisitorSettings), settings.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetVisitorSettingsQuery(int SocietyId) : IRequest<VisitorSettingsDto>;

public class VisitorSettingsQueryHandlers : IRequestHandler<GetVisitorSettingsQuery, VisitorSettingsDto>
{
    private readonly IApplicationDbContext _context;

    public VisitorSettingsQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<VisitorSettingsDto> Handle(GetVisitorSettingsQuery request, CancellationToken ct)
    {
        var settings = await _context.VisitorSettings.FirstOrDefaultAsync(s => s.SocietyId == request.SocietyId && !s.IsDeleted, ct);

        if (settings is null)
        {
            if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
            {
                throw new NotFoundException(nameof(Society), request.SocietyId);
            }

            settings = new VisitorSettings { SocietyId = request.SocietyId };
            await _context.VisitorSettings.AddAsync(settings, ct);
            await _context.SaveChangesAsync(ct);
        }

        return new VisitorSettingsDto
        {
            Id = settings.Id, SocietyId = settings.SocietyId, ApprovalRequestExpiryMinutes = settings.ApprovalRequestExpiryMinutes,
            RetentionDays = settings.RetentionDays
        };
    }
}
