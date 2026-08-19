using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Residents;

public class EmergencyContactDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public string ContactName { get; set; } = default!;
    public string Relationship { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? AlternatePhone { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateEmergencyContactCommand(
    int FlatId, string ContactName, string Relationship, string Phone, string? AlternatePhone) : IRequest<int>;

public class CreateEmergencyContactCommandValidator : AbstractValidator<CreateEmergencyContactCommand>
{
    public CreateEmergencyContactCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Relationship).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public record UpdateEmergencyContactCommand(
    int Id, string ContactName, string Relationship, string Phone, string? AlternatePhone) : IRequest<Unit>;

public class UpdateEmergencyContactCommandValidator : AbstractValidator<UpdateEmergencyContactCommand>
{
    public UpdateEmergencyContactCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Relationship).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public record DeleteEmergencyContactCommand(int Id) : IRequest<Unit>;

public class EmergencyContactCommandHandlers :
    IRequestHandler<CreateEmergencyContactCommand, int>,
    IRequestHandler<UpdateEmergencyContactCommand, Unit>,
    IRequestHandler<DeleteEmergencyContactCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public EmergencyContactCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateEmergencyContactCommand request, CancellationToken ct)
    {
        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }

        var contact = new EmergencyContact
        {
            FlatId = request.FlatId, ContactName = request.ContactName, Relationship = request.Relationship,
            Phone = request.Phone, AlternatePhone = request.AlternatePhone
        };
        await _context.EmergencyContacts.AddAsync(contact, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Residents", nameof(EmergencyContact), contact.Id.ToString(), ct: ct);
        return contact.Id;
    }

    public async Task<Unit> Handle(UpdateEmergencyContactCommand request, CancellationToken ct)
    {
        var contact = await _context.EmergencyContacts.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(EmergencyContact), request.Id);

        contact.ContactName = request.ContactName;
        contact.Relationship = request.Relationship;
        contact.Phone = request.Phone;
        contact.AlternatePhone = request.AlternatePhone;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(EmergencyContact), contact.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteEmergencyContactCommand request, CancellationToken ct)
    {
        var contact = await _context.EmergencyContacts.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(EmergencyContact), request.Id);

        contact.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Residents", nameof(EmergencyContact), contact.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetEmergencyContactsQuery(int FlatId) : IRequest<List<EmergencyContactDto>>;

public class EmergencyContactQueryHandlers : IRequestHandler<GetEmergencyContactsQuery, List<EmergencyContactDto>>
{
    private readonly IApplicationDbContext _context;

    public EmergencyContactQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<EmergencyContactDto>> Handle(GetEmergencyContactsQuery request, CancellationToken ct) =>
        await _context.EmergencyContacts
            .Where(c => c.FlatId == request.FlatId && !c.IsDeleted)
            .Select(c => new EmergencyContactDto
            {
                Id = c.Id, FlatId = c.FlatId, FlatNumber = c.Flat.FlatNumber, ContactName = c.ContactName,
                Relationship = c.Relationship, Phone = c.Phone, AlternatePhone = c.AlternatePhone
            })
            .ToListAsync(ct);
}
