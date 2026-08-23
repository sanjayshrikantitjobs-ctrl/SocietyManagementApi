using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Occupancy;

public class OccupancyMemberDto
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string PersonName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? PhotoUrl { get; set; }
    public PersonRelationship Relationship { get; set; }
    public bool IsPrimary { get; set; }
    public ResidentStatus ResidentStatus { get; set; }
    public DateTime JoinedDate { get; set; }
    public DateTime? LeftDate { get; set; }
}

public class FlatOccupancyDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public OccupancyType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public List<OccupancyMemberDto> Members { get; set; } = new();
    public RentalAgreementDto? RentalAgreement { get; set; }
}

/// <summary>The flat detail screen's top-of-page summary: current Owner and
/// current Tenant episode, each with its full member list, in one call.</summary>
public class FlatOccupancyOverviewDto
{
    public int FlatId { get; set; }
    public FlatOccupancyDto? CurrentOwnerOccupancy { get; set; }
    public FlatOccupancyDto? CurrentTenantOccupancy { get; set; }
}

// ---- Commands ----------------------------------------------------------------

/// <summary>The "Add Owner Member" dialog's target. Adds the person to the
/// flat's current Owner episode, creating that episode first if none
/// exists yet. Owners are additive within one episode (co-owners), not a
/// close-and-reopen like Tenant.</summary>
public record AddOwnerMemberCommand(
    int FlatId, int? PersonId, string? FirstName, string? LastName, string? Phone, string? Email,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl, string? AadhaarNumber, string? PanNumber,
    PersonRelationship Relationship, bool IsPrimary, DateTime MoveInDate) : IRequest<int>;

public class AddOwnerMemberCommandValidator : AbstractValidator<AddOwnerMemberCommand>
{
    public AddOwnerMemberCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("First name is required for a new person.");
        RuleFor(x => x.LastName).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("Last name is required for a new person.");
        RuleFor(x => x.Phone).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("Phone is required for a new person.");
    }
}

/// <summary>The "Add Tenant" dialog's first step — the person added becomes
/// the sole, Primary member of a brand-new Tenant episode. Auto-closes any
/// currently-open Tenant episode on the flat first (rule: only one active
/// Tenant occupancy per flat; previous auto-closes before a new one starts).</summary>
public record AddTenantOccupancyCommand(
    int FlatId, int? PersonId, string? FirstName, string? LastName, string? Phone, string? Email,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl, string? AadhaarNumber, string? PanNumber,
    DateTime MoveInDate) : IRequest<int>;

public class AddTenantOccupancyCommandValidator : AbstractValidator<AddTenantOccupancyCommand>
{
    public AddTenantOccupancyCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("First name is required for a new person.");
        RuleFor(x => x.LastName).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("Last name is required for a new person.");
        RuleFor(x => x.Phone).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("Phone is required for a new person.");
    }
}

/// <summary>"Additional residents added afterward" — never Primary.</summary>
public record AddTenantFamilyMemberCommand(
    int FlatOccupancyId, int? PersonId, string? FirstName, string? LastName, string? Phone, string? Email,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl, string? AadhaarNumber, string? PanNumber,
    PersonRelationship Relationship, DateTime MoveInDate) : IRequest<int>;

public class AddTenantFamilyMemberCommandValidator : AbstractValidator<AddTenantFamilyMemberCommand>
{
    public AddTenantFamilyMemberCommandValidator()
    {
        RuleFor(x => x.FlatOccupancyId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("First name is required for a new person.");
        RuleFor(x => x.LastName).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("Last name is required for a new person.");
        RuleFor(x => x.Phone).NotEmpty().When(x => x.PersonId == null)
            .WithMessage("Phone is required for a new person.");
    }
}

/// <summary>Closes the whole episode: sets EndDate and bulk-sets LeftDate on
/// every still-open member in one transaction — a tenant's family leaves
/// together, not as N independent edits.</summary>
public record EndOccupancyCommand(int FlatOccupancyId, DateTime EndDate) : IRequest<Unit>;

/// <summary>One person leaving early while the rest of the episode stays open.</summary>
public record RemoveOccupancyMemberCommand(int OccupancyMemberId, DateTime LeftDate) : IRequest<Unit>;

public record UpdateOccupancyMemberCommand(
    int Id, PersonRelationship Relationship, ResidentStatus ResidentStatus) : IRequest<Unit>;

public class FlatOccupancyCommandHandlers :
    IRequestHandler<AddOwnerMemberCommand, int>,
    IRequestHandler<AddTenantOccupancyCommand, int>,
    IRequestHandler<AddTenantFamilyMemberCommand, int>,
    IRequestHandler<EndOccupancyCommand, Unit>,
    IRequestHandler<RemoveOccupancyMemberCommand, Unit>,
    IRequestHandler<UpdateOccupancyMemberCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FlatOccupancyCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private async Task<int> GetFlatSocietyIdAsync(int flatId, CancellationToken ct)
    {
        var societyId = await _context.Flats
            .Where(f => f.Id == flatId && !f.IsDeleted)
            .Select(f => (int?)f.Floor.Wing.Building.SocietyId)
            .FirstOrDefaultAsync(ct);
        return societyId ?? throw new NotFoundException(nameof(Flat), flatId);
    }

    private async Task<Person> ResolveOrCreatePersonAsync(
        int societyId, int? personId, string? firstName, string? lastName, string? phone, string? email,
        Gender? gender, DateTime? dateOfBirth, string? photoUrl, string? aadhaarNumber, string? panNumber,
        CancellationToken ct)
    {
        if (personId.HasValue)
        {
            var person = await _context.People.FirstOrDefaultAsync(p => p.Id == personId.Value && !p.IsDeleted, ct)
                ?? throw new NotFoundException(nameof(Person), personId.Value);
            if (person.SocietyId != societyId)
            {
                throw new ConflictAppException("Flat and person must belong to the same society.");
            }
            return person;
        }

        if (await _context.People.AnyAsync(p => p.SocietyId == societyId && !p.IsDeleted && p.Phone == phone, ct))
        {
            throw new ConflictAppException("A person with this phone number already exists in this society. Search for them instead of creating a new one.");
        }

        var newPerson = new Person
        {
            SocietyId = societyId, FirstName = firstName!, LastName = lastName!, Phone = phone!, Email = email,
            Gender = gender, DateOfBirth = dateOfBirth, PhotoUrl = photoUrl,
            AadhaarNumber = aadhaarNumber, PanNumber = panNumber
        };
        await _context.People.AddAsync(newPerson, ct);
        await _context.SaveChangesAsync(ct);
        return newPerson;
    }

    public async Task<int> Handle(AddOwnerMemberCommand request, CancellationToken ct)
    {
        var societyId = await GetFlatSocietyIdAsync(request.FlatId, ct);
        var person = await ResolveOrCreatePersonAsync(
            societyId, request.PersonId, request.FirstName, request.LastName, request.Phone, request.Email,
            request.Gender, request.DateOfBirth, request.PhotoUrl, request.AadhaarNumber, request.PanNumber, ct);

        var occupancy = await _context.FlatOccupancies.FirstOrDefaultAsync(
            o => o.FlatId == request.FlatId && o.Type == OccupancyType.Owner && o.EndDate == null && !o.IsDeleted, ct);

        if (occupancy is null)
        {
            occupancy = new FlatOccupancy { FlatId = request.FlatId, Type = OccupancyType.Owner, StartDate = request.MoveInDate };
            await _context.FlatOccupancies.AddAsync(occupancy, ct);
            await _context.SaveChangesAsync(ct);
        }
        else if (await _context.OccupancyMembers.AnyAsync(
            m => m.FlatOccupancyId == occupancy.Id && m.PersonId == person.Id && m.LeftDate == null && !m.IsDeleted, ct))
        {
            throw new ConflictAppException("This person is already part of this occupancy.");
        }

        if (request.IsPrimary)
        {
            var settings = await _context.OccupancySettings.FirstOrDefaultAsync(s => s.SocietyId == societyId && !s.IsDeleted, ct);
            var allowMultiple = settings?.AllowMultiplePrimaryOwners ?? false;
            if (!allowMultiple && await _context.OccupancyMembers.AnyAsync(
                m => m.FlatOccupancy.FlatId == request.FlatId && m.FlatOccupancy.Type == OccupancyType.Owner
                     && m.LeftDate == null && m.IsPrimary && !m.IsDeleted, ct))
            {
                throw new ConflictAppException("This flat already has a Primary Owner. Enable multiple owners in Occupancy Settings first.");
            }
        }

        var member = new OccupancyMember
        {
            FlatOccupancyId = occupancy.Id, PersonId = person.Id, Relationship = request.Relationship,
            IsPrimary = request.IsPrimary, JoinedDate = request.MoveInDate
        };
        await _context.OccupancyMembers.AddAsync(member, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(OccupancyMember), member.Id.ToString(), ct: ct);
        return member.Id;
    }

    public async Task<int> Handle(AddTenantOccupancyCommand request, CancellationToken ct)
    {
        var societyId = await GetFlatSocietyIdAsync(request.FlatId, ct);
        var person = await ResolveOrCreatePersonAsync(
            societyId, request.PersonId, request.FirstName, request.LastName, request.Phone, request.Email,
            request.Gender, request.DateOfBirth, request.PhotoUrl, request.AadhaarNumber, request.PanNumber, ct);

        var previous = await _context.FlatOccupancies.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.FlatId == request.FlatId && o.Type == OccupancyType.Tenant && o.EndDate == null && !o.IsDeleted, ct);

        if (previous is not null)
        {
            if (request.MoveInDate < previous.StartDate)
            {
                throw new ConflictAppException("The new tenant's move-in date cannot be before the previous tenant's move-in date.");
            }

            previous.EndDate = request.MoveInDate;
            foreach (var member in previous.Members.Where(m => m.LeftDate == null))
            {
                member.LeftDate = request.MoveInDate;
            }
        }

        var occupancy = new FlatOccupancy { FlatId = request.FlatId, Type = OccupancyType.Tenant, StartDate = request.MoveInDate };
        await _context.FlatOccupancies.AddAsync(occupancy, ct);
        await _context.SaveChangesAsync(ct);

        var newMember = new OccupancyMember
        {
            FlatOccupancyId = occupancy.Id, PersonId = person.Id, Relationship = PersonRelationship.Self,
            IsPrimary = true, JoinedDate = request.MoveInDate
        };
        await _context.OccupancyMembers.AddAsync(newMember, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(FlatOccupancy), occupancy.Id.ToString(), ct: ct);
        return occupancy.Id;
    }

    public async Task<int> Handle(AddTenantFamilyMemberCommand request, CancellationToken ct)
    {
        var occupancy = await _context.FlatOccupancies.FirstOrDefaultAsync(o => o.Id == request.FlatOccupancyId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FlatOccupancy), request.FlatOccupancyId);

        if (occupancy.Type != OccupancyType.Tenant)
        {
            throw new ConflictAppException("Family members can only be added to a Tenant occupancy.");
        }
        if (occupancy.EndDate.HasValue)
        {
            throw new ConflictAppException("This occupancy has already ended.");
        }
        if (request.MoveInDate < occupancy.StartDate)
        {
            throw new ConflictAppException("Move-in date cannot be before the occupancy's start date.");
        }

        var societyId = await GetFlatSocietyIdAsync(occupancy.FlatId, ct);
        var person = await ResolveOrCreatePersonAsync(
            societyId, request.PersonId, request.FirstName, request.LastName, request.Phone, request.Email,
            request.Gender, request.DateOfBirth, request.PhotoUrl, request.AadhaarNumber, request.PanNumber, ct);

        if (await _context.OccupancyMembers.AnyAsync(
            m => m.FlatOccupancyId == occupancy.Id && m.PersonId == person.Id && m.LeftDate == null && !m.IsDeleted, ct))
        {
            throw new ConflictAppException("This person is already part of this occupancy.");
        }

        var member = new OccupancyMember
        {
            FlatOccupancyId = occupancy.Id, PersonId = person.Id, Relationship = request.Relationship,
            IsPrimary = false, JoinedDate = request.MoveInDate
        };
        await _context.OccupancyMembers.AddAsync(member, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(OccupancyMember), member.Id.ToString(), ct: ct);
        return member.Id;
    }

    public async Task<Unit> Handle(EndOccupancyCommand request, CancellationToken ct)
    {
        var occupancy = await _context.FlatOccupancies.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == request.FlatOccupancyId && !o.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FlatOccupancy), request.FlatOccupancyId);

        if (occupancy.EndDate.HasValue)
        {
            throw new ConflictAppException("This occupancy has already ended.");
        }
        if (request.EndDate < occupancy.StartDate)
        {
            throw new ConflictAppException("Move-out date cannot be before move-in date.");
        }

        occupancy.EndDate = request.EndDate;
        foreach (var member in occupancy.Members.Where(m => m.LeftDate == null))
        {
            member.LeftDate = request.EndDate;
        }

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Occupancy", nameof(FlatOccupancy), occupancy.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RemoveOccupancyMemberCommand request, CancellationToken ct)
    {
        var member = await _context.OccupancyMembers.FirstOrDefaultAsync(m => m.Id == request.OccupancyMemberId && !m.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(OccupancyMember), request.OccupancyMemberId);

        if (member.LeftDate.HasValue)
        {
            throw new ConflictAppException("This person has already left this occupancy.");
        }
        if (request.LeftDate < member.JoinedDate)
        {
            throw new ConflictAppException("Move-out date cannot be before move-in date.");
        }

        member.LeftDate = request.LeftDate;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Occupancy", nameof(OccupancyMember), member.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(UpdateOccupancyMemberCommand request, CancellationToken ct)
    {
        var member = await _context.OccupancyMembers.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(OccupancyMember), request.Id);

        member.Relationship = request.Relationship;
        member.ResidentStatus = request.ResidentStatus;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Occupancy", nameof(OccupancyMember), member.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetFlatOccupancyOverviewQuery(int FlatId) : IRequest<FlatOccupancyOverviewDto>;

public record GetOccupancyMembersQuery(int FlatOccupancyId) : IRequest<List<OccupancyMemberDto>>;

/// <summary>All past+current episodes for a flat (optionally filtered by
/// Type), each with its full — including departed — member list. Read-only:
/// there is deliberately no delete endpoint for FlatOccupancy/OccupancyMember,
/// so history can never be removed.</summary>
public record GetOccupancyHistoryQuery(int FlatId, OccupancyType? Type) : IRequest<List<FlatOccupancyDto>>;

public class FlatOccupancyQueryHandlers :
    IRequestHandler<GetFlatOccupancyOverviewQuery, FlatOccupancyOverviewDto>,
    IRequestHandler<GetOccupancyMembersQuery, List<OccupancyMemberDto>>,
    IRequestHandler<GetOccupancyHistoryQuery, List<FlatOccupancyDto>>
{
    private readonly IApplicationDbContext _context;

    public FlatOccupancyQueryHandlers(IApplicationDbContext context) => _context = context;

    private static IQueryable<OccupancyMemberDto> ProjectMembers(IQueryable<OccupancyMember> query) =>
        query.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.JoinedDate)
            .Select(m => new OccupancyMemberDto
            {
                Id = m.Id, PersonId = m.PersonId, PersonName = m.Person.FirstName + " " + m.Person.LastName,
                Phone = m.Person.Phone, Email = m.Person.Email, PhotoUrl = m.Person.PhotoUrl,
                Relationship = m.Relationship, IsPrimary = m.IsPrimary, ResidentStatus = m.ResidentStatus,
                JoinedDate = m.JoinedDate, LeftDate = m.LeftDate
            });

    private static IQueryable<FlatOccupancyDto> ProjectOccupancy(IQueryable<FlatOccupancy> query) =>
        query.Select(o => new FlatOccupancyDto
        {
            Id = o.Id, FlatId = o.FlatId, Type = o.Type, StartDate = o.StartDate, EndDate = o.EndDate, Notes = o.Notes,
            Members = o.Members.Where(m => !m.IsDeleted).OrderByDescending(m => m.IsPrimary).ThenBy(m => m.JoinedDate)
                .Select(m => new OccupancyMemberDto
                {
                    Id = m.Id, PersonId = m.PersonId, PersonName = m.Person.FirstName + " " + m.Person.LastName,
                    Phone = m.Person.Phone, Email = m.Person.Email, PhotoUrl = m.Person.PhotoUrl,
                    Relationship = m.Relationship, IsPrimary = m.IsPrimary, ResidentStatus = m.ResidentStatus,
                    JoinedDate = m.JoinedDate, LeftDate = m.LeftDate
                }).ToList(),
            RentalAgreement = o.RentalAgreement == null ? null : new RentalAgreementDto
            {
                Id = o.RentalAgreement.Id, FlatOccupancyId = o.RentalAgreement.FlatOccupancyId,
                AgreementStartDate = o.RentalAgreement.AgreementStartDate, AgreementEndDate = o.RentalAgreement.AgreementEndDate,
                SecurityDeposit = o.RentalAgreement.SecurityDeposit, RentAmount = o.RentalAgreement.RentAmount,
                PoliceVerificationStatus = o.RentalAgreement.PoliceVerificationStatus,
                PoliceVerificationReference = o.RentalAgreement.PoliceVerificationReference,
                AgreementDocumentUrl = o.RentalAgreement.AgreementDocumentUrl
            }
        });

    public async Task<FlatOccupancyOverviewDto> Handle(GetFlatOccupancyOverviewQuery request, CancellationToken ct)
    {
        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }

        var owner = await ProjectOccupancy(_context.FlatOccupancies.Where(
            o => o.FlatId == request.FlatId && o.Type == OccupancyType.Owner && o.EndDate == null && !o.IsDeleted))
            .FirstOrDefaultAsync(ct);

        var tenant = await ProjectOccupancy(_context.FlatOccupancies.Where(
            o => o.FlatId == request.FlatId && o.Type == OccupancyType.Tenant && o.EndDate == null && !o.IsDeleted))
            .FirstOrDefaultAsync(ct);

        return new FlatOccupancyOverviewDto { FlatId = request.FlatId, CurrentOwnerOccupancy = owner, CurrentTenantOccupancy = tenant };
    }

    public async Task<List<OccupancyMemberDto>> Handle(GetOccupancyMembersQuery request, CancellationToken ct) =>
        await ProjectMembers(_context.OccupancyMembers.Where(m => m.FlatOccupancyId == request.FlatOccupancyId && !m.IsDeleted))
            .ToListAsync(ct);

    public async Task<List<FlatOccupancyDto>> Handle(GetOccupancyHistoryQuery request, CancellationToken ct)
    {
        var query = _context.FlatOccupancies.Where(o => o.FlatId == request.FlatId && !o.IsDeleted);
        if (request.Type.HasValue)
        {
            query = query.Where(o => o.Type == request.Type.Value);
        }
        return await ProjectOccupancy(query.OrderByDescending(o => o.StartDate)).ToListAsync(ct);
    }
}
