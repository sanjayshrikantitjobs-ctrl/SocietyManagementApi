using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Occupancy;

public class OccupancySettingsDto
{
    public int SocietyId { get; set; }
    public bool AllowMultiplePrimaryOwners { get; set; }
}

public record GetOccupancySettingsQuery(int SocietyId) : IRequest<OccupancySettingsDto>;

public record UpdateOccupancySettingsCommand(int SocietyId, bool AllowMultiplePrimaryOwners) : IRequest<Unit>;

public class OccupancySettingsQueryHandler : IRequestHandler<GetOccupancySettingsQuery, OccupancySettingsDto>
{
    private readonly IApplicationDbContext _context;

    public OccupancySettingsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<OccupancySettingsDto> Handle(GetOccupancySettingsQuery request, CancellationToken ct)
    {
        var settings = await _context.OccupancySettings
            .FirstOrDefaultAsync(s => s.SocietyId == request.SocietyId && !s.IsDeleted, ct);

        return new OccupancySettingsDto
        {
            SocietyId = request.SocietyId,
            AllowMultiplePrimaryOwners = settings?.AllowMultiplePrimaryOwners ?? false
        };
    }
}

public class UpdateOccupancySettingsCommandHandler : IRequestHandler<UpdateOccupancySettingsCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public UpdateOccupancySettingsCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(UpdateOccupancySettingsCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var settings = await _context.OccupancySettings.FirstOrDefaultAsync(s => s.SocietyId == request.SocietyId && !s.IsDeleted, ct);
        if (settings is null)
        {
            settings = new OccupancySettings { SocietyId = request.SocietyId };
            await _context.OccupancySettings.AddAsync(settings, ct);
        }

        settings.AllowMultiplePrimaryOwners = request.AllowMultiplePrimaryOwners;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Occupancy", nameof(OccupancySettings), settings.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}
