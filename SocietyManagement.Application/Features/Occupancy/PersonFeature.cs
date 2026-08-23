using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Occupancy;

public class PersonDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public Gender? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? PhotoUrl { get; set; }
    public string? AadhaarNumber { get; set; }
    public string? PanNumber { get; set; }
}

public class OccupancyMembershipSummaryDto
{
    public int FlatOccupancyId { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public OccupancyType Type { get; set; }
    public PersonRelationship Relationship { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime JoinedDate { get; set; }
    public DateTime? LeftDate { get; set; }
}

public class PersonDetailDto : PersonDto
{
    public List<OccupancyMembershipSummaryDto> Memberships { get; set; } = new();
}

// ---- Commands ----------------------------------------------------------------
public record CreatePersonCommand(
    int SocietyId, string FirstName, string LastName, string Phone, string? Email,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl, string? AadhaarNumber, string? PanNumber) : IRequest<int>;

public class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record UpdatePersonCommand(
    int Id, string FirstName, string LastName, string Phone, string? Email,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl, string? AadhaarNumber, string? PanNumber) : IRequest<Unit>;

public class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class PersonCommandHandlers :
    IRequestHandler<CreatePersonCommand, int>,
    IRequestHandler<UpdatePersonCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public PersonCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreatePersonCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }
        if (await _context.People.AnyAsync(p => p.SocietyId == request.SocietyId && !p.IsDeleted && p.Phone == request.Phone, ct))
        {
            throw new ConflictAppException("A person with this phone number already exists in this society.");
        }

        var person = new Person
        {
            SocietyId = request.SocietyId, FirstName = request.FirstName, LastName = request.LastName,
            Phone = request.Phone, Email = request.Email, Gender = request.Gender,
            DateOfBirth = request.DateOfBirth, PhotoUrl = request.PhotoUrl,
            AadhaarNumber = request.AadhaarNumber, PanNumber = request.PanNumber
        };
        await _context.People.AddAsync(person, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(Person), person.Id.ToString(), ct: ct);
        return person.Id;
    }

    public async Task<Unit> Handle(UpdatePersonCommand request, CancellationToken ct)
    {
        var person = await _context.People.FirstOrDefaultAsync(p => p.Id == request.Id && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Person), request.Id);

        if (await _context.People.AnyAsync(
            p => p.SocietyId == person.SocietyId && p.Id != person.Id && !p.IsDeleted && p.Phone == request.Phone, ct))
        {
            throw new ConflictAppException("A person with this phone number already exists in this society.");
        }

        person.FirstName = request.FirstName;
        person.LastName = request.LastName;
        person.Phone = request.Phone;
        person.Email = request.Email;
        person.Gender = request.Gender;
        person.DateOfBirth = request.DateOfBirth;
        person.PhotoUrl = request.PhotoUrl;
        person.AadhaarNumber = request.AadhaarNumber;
        person.PanNumber = request.PanNumber;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Occupancy", nameof(Person), person.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
/// <summary>The "reusable person" lookup — the Add Owner Member / Add Tenant
/// dialogs call this on phone-blur so an existing Person can be attached
/// instead of a duplicate being created.</summary>
public record SearchPersonsQuery(int SocietyId, string Phone) : IRequest<PersonDto?>;

public record GetPersonByIdQuery(int Id) : IRequest<PersonDetailDto>;

public class PersonQueryHandlers :
    IRequestHandler<SearchPersonsQuery, PersonDto?>,
    IRequestHandler<GetPersonByIdQuery, PersonDetailDto>
{
    private readonly IApplicationDbContext _context;

    public PersonQueryHandlers(IApplicationDbContext context) => _context = context;

    private static IQueryable<PersonDto> Project(IQueryable<Person> query) =>
        query.Select(p => new PersonDto
        {
            Id = p.Id, SocietyId = p.SocietyId, FirstName = p.FirstName, LastName = p.LastName, Phone = p.Phone,
            Email = p.Email, Gender = p.Gender, DateOfBirth = p.DateOfBirth, PhotoUrl = p.PhotoUrl,
            AadhaarNumber = p.AadhaarNumber, PanNumber = p.PanNumber
        });

    public async Task<PersonDto?> Handle(SearchPersonsQuery request, CancellationToken ct) =>
        await Project(_context.People.Where(p => p.SocietyId == request.SocietyId && !p.IsDeleted && p.Phone == request.Phone))
            .FirstOrDefaultAsync(ct);

    public async Task<PersonDetailDto> Handle(GetPersonByIdQuery request, CancellationToken ct)
    {
        var person = await Project(_context.People.Where(p => p.Id == request.Id && !p.IsDeleted))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Person), request.Id);

        var memberships = await _context.OccupancyMembers
            .Where(m => m.PersonId == request.Id && !m.IsDeleted)
            .OrderByDescending(m => m.JoinedDate)
            .Select(m => new OccupancyMembershipSummaryDto
            {
                FlatOccupancyId = m.FlatOccupancyId, FlatId = m.FlatOccupancy.FlatId,
                FlatNumber = m.FlatOccupancy.Flat.FlatNumber, Type = m.FlatOccupancy.Type,
                Relationship = m.Relationship, IsPrimary = m.IsPrimary, JoinedDate = m.JoinedDate, LeftDate = m.LeftDate
            })
            .ToListAsync(ct);

        return new PersonDetailDto
        {
            Id = person.Id, SocietyId = person.SocietyId, FirstName = person.FirstName, LastName = person.LastName,
            Phone = person.Phone, Email = person.Email, Gender = person.Gender, DateOfBirth = person.DateOfBirth,
            PhotoUrl = person.PhotoUrl, AadhaarNumber = person.AadhaarNumber, PanNumber = person.PanNumber,
            Memberships = memberships
        };
    }
}
