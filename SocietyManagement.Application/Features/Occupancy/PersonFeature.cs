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
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? WhatsAppNumber { get; set; }
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

public class PersonLoginDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = default!;
    public string MobileNumber { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public bool IsActive { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreatePersonCommand(
    int SocietyId, string FirstName, string LastName, string Phone, string? Email, string? WhatsAppNumber,
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
    int Id, string FirstName, string LastName, string Phone, string? Email, string? WhatsAppNumber,
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

/// <summary>Gives an existing Person login access, keyed off Person/
/// User.PersonId instead of Member/Member.UserId. FlatId names which flat
/// this login is "for" — the username is derived from it
/// ({flatNumber}_{firstName}) since a Person can have login access created
/// from more than one flat context (e.g. after moving) and there's no
/// other natural per-login identifier to build a username from.</summary>
public record CreateUserForPersonCommand(int PersonId, int FlatId, int RoleId, string? Password = null) : IRequest<int>;

public class CreateUserForPersonCommandValidator : AbstractValidator<CreateUserForPersonCommand>
{
    public CreateUserForPersonCommandValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.RoleId).GreaterThan(0);
        RuleFor(x => x.Password)
            .MinimumLength(8).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a special character.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}

/// <summary>One flat's worth of results from a bulk login-creation run —
/// created/skipped, never throws for an individual flat's failure so one
/// bad flat doesn't abort the whole batch.</summary>
public record BulkLoginResultDto(int FlatId, string FlatNumber, string? PersonName, bool Created, string? SkipReason);

/// <summary>Admin picks a page of flats from the Owners grid and creates a
/// login for each flat's current Primary Owner in one action, instead of
/// opening "Create Login" once per flat. Reuses the exact same
/// username/password rules as the single-flat flow.</summary>
public record BulkCreateOwnerLoginsCommand(List<int> FlatIds, int RoleId, string? Password = null) : IRequest<List<BulkLoginResultDto>>;

public class BulkCreateOwnerLoginsCommandValidator : AbstractValidator<BulkCreateOwnerLoginsCommand>
{
    public BulkCreateOwnerLoginsCommandValidator()
    {
        RuleFor(x => x.FlatIds).NotEmpty();
        RuleFor(x => x.RoleId).GreaterThan(0);
        RuleFor(x => x.Password)
            .MinimumLength(8).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a special character.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}

public class PersonCommandHandlers :
    IRequestHandler<CreatePersonCommand, int>,
    IRequestHandler<UpdatePersonCommand, Unit>,
    IRequestHandler<CreateUserForPersonCommand, int>,
    IRequestHandler<BulkCreateOwnerLoginsCommand, List<BulkLoginResultDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public PersonCommandHandlers(
        IApplicationDbContext context, IAuditService auditService,
        IPasswordHasher passwordHasher, IEmailService emailService)
    {
        _context = context;
        _auditService = auditService;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
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
            Phone = request.Phone, Email = request.Email, WhatsAppNumber = request.WhatsAppNumber, Gender = request.Gender,
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
        person.WhatsAppNumber = request.WhatsAppNumber;
        person.Gender = request.Gender;
        person.DateOfBirth = request.DateOfBirth;
        person.PhotoUrl = request.PhotoUrl;
        person.AadhaarNumber = request.AadhaarNumber;
        person.PanNumber = request.PanNumber;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Occupancy", nameof(Person), person.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<int> Handle(CreateUserForPersonCommand request, CancellationToken ct)
    {
        var person = await _context.People.FirstOrDefaultAsync(p => p.Id == request.PersonId && !p.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Person), request.PersonId);
        var flat = await _context.Flats.FirstOrDefaultAsync(fl => fl.Id == request.FlatId && !fl.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Flat), request.FlatId);

        if (await _context.Users.AnyAsync(u => u.PersonId == person.Id && !u.IsDeleted, ct))
        {
            throw new ConflictAppException("This person already has login access.");
        }
        if (!await _context.Roles.AnyAsync(r => r.Id == request.RoleId && !r.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Role), request.RoleId);
        }

        var user = await CreateLoginForPersonAsync(person, flat.FlatNumber, request.RoleId, request.Password, ct);
        await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(User), user.Id.ToString(), ct: ct);
        return user.Id;
    }

    public async Task<List<BulkLoginResultDto>> Handle(BulkCreateOwnerLoginsCommand request, CancellationToken ct)
    {
        if (!await _context.Roles.AnyAsync(r => r.Id == request.RoleId && !r.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Role), request.RoleId);
        }

        var flats = await _context.Flats
            .Where(fl => request.FlatIds.Contains(fl.Id) && !fl.IsDeleted)
            .Select(fl => new { fl.Id, fl.FlatNumber })
            .ToListAsync(ct);

        var results = new List<BulkLoginResultDto>();
        foreach (var flatId in request.FlatIds)
        {
            var flat = flats.FirstOrDefault(fl => fl.Id == flatId);
            if (flat is null)
            {
                results.Add(new BulkLoginResultDto(flatId, "?", null, false, "Flat not found."));
                continue;
            }

            var primaryOwner = await _context.OccupancyMembers
                .Where(m => m.FlatOccupancy.FlatId == flatId && m.FlatOccupancy.Type == OccupancyType.Owner
                    && m.FlatOccupancy.EndDate == null && m.LeftDate == null && m.IsPrimary && !m.IsDeleted)
                .Select(m => m.Person)
                .FirstOrDefaultAsync(ct);

            if (primaryOwner is null)
            {
                results.Add(new BulkLoginResultDto(flatId, flat.FlatNumber, null, false, "No primary owner on file."));
                continue;
            }
            if (await _context.Users.AnyAsync(u => u.PersonId == primaryOwner.Id && !u.IsDeleted, ct))
            {
                results.Add(new BulkLoginResultDto(flatId, flat.FlatNumber, primaryOwner.FullName, false, "Already has a login."));
                continue;
            }

            try
            {
                var user = await CreateLoginForPersonAsync(primaryOwner, flat.FlatNumber, request.RoleId, request.Password, ct);
                await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(User), user.Id.ToString(), ct: ct);
                results.Add(new BulkLoginResultDto(flatId, flat.FlatNumber, primaryOwner.FullName, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new BulkLoginResultDto(flatId, flat.FlatNumber, primaryOwner.FullName, false, ex.Message));
            }
        }

        return results;
    }

    /// <summary>Shared by the single-flat and bulk login flows. Username is
    /// {flatNumber}_{firstName} (lowercased, spaces stripped) with a numeric
    /// suffix on collision — deliberately not tied to Person.Email, so a
    /// person with no real email still gets a predictable, memorable
    /// username instead of the old phone-placeholder scheme. Blank password
    /// defaults to the well-known "Test@12345" (not a random one) so bulk
    /// runs don't need per-account password distribution; MustChangePassword
    /// still forces a change on first login either way.</summary>
    private async Task<User> CreateLoginForPersonAsync(
        Person person, string flatNumber, int roleId, string? requestedPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(person.Phone))
        {
            throw new ConflictAppException($"{person.FullName} has no phone number on file — a login account needs one. Add a phone number first.");
        }

        var baseUsername = $"{flatNumber}_{person.FirstName.Trim().Replace(" ", "")}".ToLowerInvariant();
        var username = baseUsername;
        var suffix = 2;
        while (await _context.Users.AnyAsync(u => u.Email == username && !u.IsDeleted, ct))
        {
            username = $"{baseUsername}_{suffix}";
            suffix++;
        }

        var adminSetPassword = !string.IsNullOrEmpty(requestedPassword);
        var password = adminSetPassword ? requestedPassword! : DefaultPassword;

        var user = new User
        {
            FirstName = person.FirstName, LastName = person.LastName, Email = username,
            MobileNumber = person.Phone, RoleId = roleId, PersonId = person.Id, SocietyId = person.SocietyId,
            PasswordHash = _passwordHasher.Hash(password), MustChangePassword = !adminSetPassword, IsActive = true
        };
        await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);

        if (!adminSetPassword && !string.IsNullOrWhiteSpace(person.Email))
        {
            await _emailService.SendEmailAsync(
                person.Email!,
                "Your Society Management account has been created",
                $"<p>Hello {user.FirstName},</p>" +
                $"<p>An account has been created for you.</p>" +
                $"<p>Username: <b>{username}</b><br/>Password: <b>{password}</b></p>" +
                "<p>You will be asked to set a new password on first login.</p>",
                ct);
        }

        return user;
    }

    private const string DefaultPassword = "Test@12345";
}

// ---- Queries -------------------------------------------------------------------
/// <summary>The "reusable person" lookup — the Add Owner Member / Add Tenant
/// dialogs call this on phone-blur so an existing Person can be attached
/// instead of a duplicate being created.</summary>
public record SearchPersonsQuery(int SocietyId, string Phone) : IRequest<PersonDto?>;

public record GetPersonByIdQuery(int Id) : IRequest<PersonDetailDto>;

public record GetPersonLoginQuery(int PersonId) : IRequest<PersonLoginDto?>;

public class PersonQueryHandlers :
    IRequestHandler<SearchPersonsQuery, PersonDto?>,
    IRequestHandler<GetPersonByIdQuery, PersonDetailDto>,
    IRequestHandler<GetPersonLoginQuery, PersonLoginDto?>
{
    private readonly IApplicationDbContext _context;

    public PersonQueryHandlers(IApplicationDbContext context) => _context = context;

    private static IQueryable<PersonDto> Project(IQueryable<Person> query) =>
        query.Select(p => new PersonDto
        {
            Id = p.Id, SocietyId = p.SocietyId, FirstName = p.FirstName, LastName = p.LastName, Phone = p.Phone,
            Email = p.Email, WhatsAppNumber = p.WhatsAppNumber, Gender = p.Gender, DateOfBirth = p.DateOfBirth, PhotoUrl = p.PhotoUrl,
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
            Phone = person.Phone, Email = person.Email, WhatsAppNumber = person.WhatsAppNumber, Gender = person.Gender,
            DateOfBirth = person.DateOfBirth, PhotoUrl = person.PhotoUrl, AadhaarNumber = person.AadhaarNumber,
            PanNumber = person.PanNumber, Memberships = memberships
        };
    }

    public async Task<PersonLoginDto?> Handle(GetPersonLoginQuery request, CancellationToken ct) =>
        await _context.Users
            .Where(u => u.PersonId == request.PersonId && !u.IsDeleted)
            .Select(u => new PersonLoginDto
            {
                UserId = u.Id, Email = u.Email, MobileNumber = u.MobileNumber, RoleName = u.Role.Name, IsActive = u.IsActive
            })
            .FirstOrDefaultAsync(ct);
}
