using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Societies.Commands;

// ---- Create ----------------------------------------------------------------
public record CreateSocietyCommand(
    string Name, string? RegistrationNumber, string Address, string City, string State, string Pincode,
    string? ContactEmail, string? ContactPhone, DateTime? EstablishedDate) : IRequest<int>;

public class CreateSocietyCommandValidator : AbstractValidator<CreateSocietyCommand>
{
    public CreateSocietyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.State).NotEmpty();
        RuleFor(x => x.Pincode).NotEmpty().Length(6);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrEmpty(x.ContactEmail));
    }
}

public class CreateSocietyCommandHandler : IRequestHandler<CreateSocietyCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public CreateSocietyCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateSocietyCommand request, CancellationToken ct)
    {
        var society = new Society
        {
            Name = request.Name,
            RegistrationNumber = request.RegistrationNumber,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            EstablishedDate = request.EstablishedDate
        };
        await _context.Societies.AddAsync(society, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Create, "Society", nameof(Society),
            society.Id.ToString(), ct: ct);
        return society.Id;
    }
}

// ---- Update ----------------------------------------------------------------
public record UpdateSocietyCommand(
    int Id, string Name, string? RegistrationNumber, string Address, string City, string State, string Pincode,
    string? ContactEmail, string? ContactPhone, DateTime? EstablishedDate) : IRequest<Unit>;

public class UpdateSocietyCommandValidator : AbstractValidator<UpdateSocietyCommand>
{
    public UpdateSocietyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty();
        RuleFor(x => x.Pincode).NotEmpty().Length(6);
    }
}

public class UpdateSocietyCommandHandler : IRequestHandler<UpdateSocietyCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public UpdateSocietyCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(UpdateSocietyCommand request, CancellationToken ct)
    {
        var society = await _context.Societies.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Society), request.Id);

        society.Name = request.Name;
        society.RegistrationNumber = request.RegistrationNumber;
        society.Address = request.Address;
        society.City = request.City;
        society.State = request.State;
        society.Pincode = request.Pincode;
        society.ContactEmail = request.ContactEmail;
        society.ContactPhone = request.ContactPhone;
        society.EstablishedDate = request.EstablishedDate;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Society", nameof(Society),
            society.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Delete ------------------------------------------------------------------
public record DeleteSocietyCommand(int Id) : IRequest<Unit>;

public class DeleteSocietyCommandHandler : IRequestHandler<DeleteSocietyCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public DeleteSocietyCommandHandler(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Unit> Handle(DeleteSocietyCommand request, CancellationToken ct)
    {
        var society = await _context.Societies
            .Include(s => s.Buildings)
            .FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Society), request.Id);

        if (society.Buildings.Any(b => !b.IsDeleted))
        {
            throw new ConflictAppException("Cannot delete a society that still has buildings. Remove buildings first.");
        }

        society.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Delete, "Society", nameof(Society),
            society.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}
