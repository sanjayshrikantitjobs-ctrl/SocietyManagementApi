using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Residents;

public class MemberDto
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
    public int? UserId { get; set; }
}

public class ResidencySummaryDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public MemberType MemberType { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public bool IsPrimaryContact { get; set; }
}

public class MemberDetailDto : MemberDto
{
    public List<ResidencySummaryDto> Residencies { get; set; } = new();
    public List<VehicleDto> Vehicles { get; set; } = new();
}

// ---- Commands ----------------------------------------------------------------
public record CreateMemberCommand(
    int SocietyId, string FirstName, string LastName, string Phone, string? Email,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl) : IRequest<int>;

public class CreateMemberCommandValidator : AbstractValidator<CreateMemberCommand>
{
    public CreateMemberCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record UpdateMemberCommand(
    int Id, string FirstName, string LastName, string Phone, string? Email,
    Gender? Gender, DateTime? DateOfBirth, string? PhotoUrl) : IRequest<Unit>;

public class UpdateMemberCommandValidator : AbstractValidator<UpdateMemberCommand>
{
    public UpdateMemberCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record DeleteMemberCommand(int Id) : IRequest<Unit>;

/// <summary>Gives an existing Member login access — reuses CreateUserCommand's
/// temp-password/email pattern (Features/Users/Commands/CreateUser) rather than
/// building a separate invite-token system, then links Member.UserId. Email is
/// optional: a member with no email gets a placeholder login address (User.Email
/// is NOT NULL in the schema) since login and notifications throughout this app
/// already work off mobile number. If <see cref="Password"/> is supplied the
/// admin's chosen password is used directly instead of an auto-generated one.</summary>
public record CreateUserForMemberCommand(int MemberId, int RoleId, string? Password = null) : IRequest<int>;

public class CreateUserForMemberCommandValidator : AbstractValidator<CreateUserForMemberCommand>
{
    public CreateUserForMemberCommandValidator()
    {
        RuleFor(x => x.MemberId).GreaterThan(0);
        RuleFor(x => x.RoleId).GreaterThan(0);
        RuleFor(x => x.Password)
            .MinimumLength(8).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit and a special character.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}

public class MemberCommandHandlers :
    IRequestHandler<CreateMemberCommand, int>,
    IRequestHandler<UpdateMemberCommand, Unit>,
    IRequestHandler<DeleteMemberCommand, Unit>,
    IRequestHandler<CreateUserForMemberCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public MemberCommandHandlers(
        IApplicationDbContext context, IAuditService auditService,
        IPasswordHasher passwordHasher, IEmailService emailService)
    {
        _context = context;
        _auditService = auditService;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<int> Handle(CreateMemberCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var member = new Member
        {
            SocietyId = request.SocietyId, FirstName = request.FirstName, LastName = request.LastName,
            Phone = request.Phone, Email = request.Email, Gender = request.Gender,
            DateOfBirth = request.DateOfBirth, PhotoUrl = request.PhotoUrl
        };
        await _context.Members.AddAsync(member, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Residents", nameof(Member), member.Id.ToString(), ct: ct);
        return member.Id;
    }

    public async Task<Unit> Handle(UpdateMemberCommand request, CancellationToken ct)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Member), request.Id);

        member.FirstName = request.FirstName;
        member.LastName = request.LastName;
        member.Phone = request.Phone;
        member.Email = request.Email;
        member.Gender = request.Gender;
        member.DateOfBirth = request.DateOfBirth;
        member.PhotoUrl = request.PhotoUrl;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Residents", nameof(Member), member.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteMemberCommand request, CancellationToken ct)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Member), request.Id);

        if (await _context.FlatResidencies.AnyAsync(r => r.MemberId == member.Id && !r.IsDeleted && r.MoveOutDate == null, ct))
        {
            throw new ConflictAppException("Cannot delete a member with a current residency. End it first.");
        }

        member.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Residents", nameof(Member), member.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<int> Handle(CreateUserForMemberCommand request, CancellationToken ct)
    {
        var member = await _context.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId && !m.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Member), request.MemberId);

        if (member.UserId.HasValue)
        {
            throw new ConflictAppException("This member already has login access.");
        }
        if (!await _context.Roles.AnyAsync(r => r.Id == request.RoleId && !r.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Role), request.RoleId);
        }

        var hasRealEmail = !string.IsNullOrWhiteSpace(member.Email);
        var loginEmail = hasRealEmail ? member.Email! : $"{member.Phone}@no-email.societymanagement.local";
        if (await _context.Users.AnyAsync(u => u.Email == loginEmail && !u.IsDeleted, ct))
        {
            throw new ConflictAppException(hasRealEmail
                ? "A user with this email already exists."
                : "This member already has a login (matched by mobile number).");
        }

        var adminSetPassword = !string.IsNullOrEmpty(request.Password);
        var password = adminSetPassword ? request.Password! : GenerateTemporaryPassword();

        var user = new User
        {
            FirstName = member.FirstName, LastName = member.LastName, Email = loginEmail,
            MobileNumber = member.Phone, RoleId = request.RoleId,
            PasswordHash = _passwordHasher.Hash(password), MustChangePassword = !adminSetPassword, IsActive = true
        };
        await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);

        member.UserId = user.Id;
        await _context.SaveChangesAsync(ct);

        // Only the auto-generated-password path needs emailing — an admin-set
        // password is already known to the admin, and a placeholder login email
        // (no real address on file) has nowhere to send to.
        if (!adminSetPassword && hasRealEmail)
        {
            await _emailService.SendEmailAsync(
                user.Email,
                "Your Society Management account has been created",
                $"<p>Hello {user.FirstName},</p>" +
                $"<p>An account has been created for you. Your temporary password is: <b>{password}</b></p>" +
                "<p>You will be asked to set a new password on first login.</p>",
                ct);
        }

        await _auditService.LogAsync(AuditAction.Create, "Residents", nameof(User), user.Id.ToString(), ct: ct);
        return user.Id;
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
        var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
        var result = new char[12];
        for (var i = 0; i < 12; i++)
        {
            result[i] = chars[random[i] % chars.Length];
        }
        return new string(result);
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetMembersQuery(
    int SocietyId, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<MemberDto>>;

public record GetMemberByIdQuery(int Id) : IRequest<MemberDetailDto>;

public class MemberQueryHandlers :
    IRequestHandler<GetMembersQuery, PaginatedResult<MemberDto>>,
    IRequestHandler<GetMemberByIdQuery, MemberDetailDto>
{
    private readonly IApplicationDbContext _context;

    public MemberQueryHandlers(IApplicationDbContext context) => _context = context;

    private static MemberDto Project(Member m) => new()
    {
        Id = m.Id, SocietyId = m.SocietyId, FirstName = m.FirstName, LastName = m.LastName, Phone = m.Phone,
        Email = m.Email, Gender = m.Gender, DateOfBirth = m.DateOfBirth, PhotoUrl = m.PhotoUrl, UserId = m.UserId
    };

    public async Task<PaginatedResult<MemberDto>> Handle(GetMembersQuery request, CancellationToken ct)
    {
        var query = _context.Members.Where(m => m.SocietyId == request.SocietyId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(m => m.FirstName.ToLower().Contains(term) || m.LastName.ToLower().Contains(term) || m.Phone.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MemberDto
            {
                Id = m.Id, SocietyId = m.SocietyId, FirstName = m.FirstName, LastName = m.LastName, Phone = m.Phone,
                Email = m.Email, Gender = m.Gender, DateOfBirth = m.DateOfBirth, PhotoUrl = m.PhotoUrl, UserId = m.UserId
            })
            .ToListAsync(ct);

        return new PaginatedResult<MemberDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<MemberDetailDto> Handle(GetMemberByIdQuery request, CancellationToken ct)
    {
        var member = await _context.Members
            .Include(m => m.Residencies).ThenInclude(r => r.Flat)
            .Include(m => m.Vehicles)
            .FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Member), request.Id);

        var dto = Project(member);
        return new MemberDetailDto
        {
            Id = dto.Id, SocietyId = dto.SocietyId, FirstName = dto.FirstName, LastName = dto.LastName, Phone = dto.Phone,
            Email = dto.Email, Gender = dto.Gender, DateOfBirth = dto.DateOfBirth, PhotoUrl = dto.PhotoUrl, UserId = dto.UserId,
            Residencies = member.Residencies.Where(r => !r.IsDeleted).OrderByDescending(r => r.MoveInDate).Select(r => new ResidencySummaryDto
            {
                Id = r.Id, FlatId = r.FlatId, FlatNumber = r.Flat.FlatNumber, MemberType = r.MemberType,
                MoveInDate = r.MoveInDate, MoveOutDate = r.MoveOutDate, IsPrimaryContact = r.IsPrimaryContact
            }).ToList(),
            Vehicles = member.Vehicles.Where(v => !v.IsDeleted).Select(v => new VehicleDto
            {
                Id = v.Id, MemberId = v.MemberId, VehicleType = v.VehicleType, RegistrationNumber = v.RegistrationNumber,
                Make = v.Make, Model = v.Model, Color = v.Color, ParkingSlotId = v.ParkingSlotId
            }).ToList()
        };
    }
}
