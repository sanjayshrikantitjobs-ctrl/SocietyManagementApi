using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalVolunteerDto
{
    public int Id { get; set; }
    public int FestivalId { get; set; }
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateVolunteerCommand(int FestivalId, string Name, string? Phone, string? Email, string? Notes) : IRequest<int>;

public class CreateVolunteerCommandValidator : AbstractValidator<CreateVolunteerCommand>
{
    public CreateVolunteerCommandValidator()
    {
        RuleFor(x => x.FestivalId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record UpdateVolunteerCommand(int Id, string Name, string? Phone, string? Email, string? Notes) : IRequest<Unit>;

public class UpdateVolunteerCommandValidator : AbstractValidator<UpdateVolunteerCommand>
{
    public UpdateVolunteerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record DeleteVolunteerCommand(int Id) : IRequest<Unit>;

public class FestivalVolunteerCommandHandlers :
    IRequestHandler<CreateVolunteerCommand, int>,
    IRequestHandler<UpdateVolunteerCommand, Unit>,
    IRequestHandler<DeleteVolunteerCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FestivalVolunteerCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateVolunteerCommand request, CancellationToken ct)
    {
        if (!await _context.Festivals.AnyAsync(f => f.Id == request.FestivalId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Festival), request.FestivalId);
        }

        var volunteer = new FestivalVolunteer
        {
            FestivalId = request.FestivalId, Name = request.Name, Phone = request.Phone,
            Email = request.Email, Notes = request.Notes
        };
        await _context.FestivalVolunteers.AddAsync(volunteer, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalVolunteer), volunteer.Id.ToString(), ct: ct);
        return volunteer.Id;
    }

    public async Task<Unit> Handle(UpdateVolunteerCommand request, CancellationToken ct)
    {
        var volunteer = await _context.FestivalVolunteers.FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalVolunteer), request.Id);

        volunteer.Name = request.Name;
        volunteer.Phone = request.Phone;
        volunteer.Email = request.Email;
        volunteer.Notes = request.Notes;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalVolunteer), volunteer.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteVolunteerCommand request, CancellationToken ct)
    {
        var volunteer = await _context.FestivalVolunteers.FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalVolunteer), request.Id);

        volunteer.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalVolunteer), volunteer.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetVolunteersQuery(int FestivalId) : IRequest<List<FestivalVolunteerDto>>;

public class FestivalVolunteerQueryHandlers : IRequestHandler<GetVolunteersQuery, List<FestivalVolunteerDto>>
{
    private readonly IApplicationDbContext _context;

    public FestivalVolunteerQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<FestivalVolunteerDto>> Handle(GetVolunteersQuery request, CancellationToken ct) =>
        await _context.FestivalVolunteers
            .Where(v => v.FestivalId == request.FestivalId && !v.IsDeleted)
            .Select(v => new FestivalVolunteerDto
            {
                Id = v.Id, FestivalId = v.FestivalId, Name = v.Name, Phone = v.Phone, Email = v.Email, Notes = v.Notes
            })
            .OrderBy(v => v.Name)
            .ToListAsync(ct);
}
