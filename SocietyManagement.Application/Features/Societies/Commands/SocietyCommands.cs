using System.Linq;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Societies.Commands;

// ---- Create ----------------------------------------------------------------
// SubscriptionStartDate/EndDate are required, not optional — every society,
// including ones created before this feature existed, carries an explicit
// subscription window (see the migration backfill for those). Society.Create
// is already Super Admin-only (see Permissions.Society.Create), so no extra
// authorization check is needed here the way SetSocietySubscriptionCommand
// needs one below.
public record CreateSocietyCommand(
    string Name, string? RegistrationNumber, string Address, string City, string State, string Pincode,
    string? ContactEmail, string? ContactPhone, DateTime? EstablishedDate,
    DateTime SubscriptionStartDate, DateTime SubscriptionEndDate, string? LogoUrl = null) : IRequest<int>;

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
        RuleFor(x => x.SubscriptionEndDate).GreaterThan(x => x.SubscriptionStartDate);
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
            Code = await GenerateUniqueCodeAsync(ct),
            RegistrationNumber = request.RegistrationNumber,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            EstablishedDate = request.EstablishedDate,
            SubscriptionStartDate = request.SubscriptionStartDate,
            SubscriptionEndDate = request.SubscriptionEndDate,
            LogoUrl = request.LogoUrl
        };
        await _context.Societies.AddAsync(society, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Create, "Society", nameof(Society),
            society.Id.ToString(), ct: ct);
        return society.Id;
    }

    /// <summary>Not client-supplied — a login-time secret should never be
    /// something the caller creating the society gets to pick sight-unseen
    /// on day one; Update still allows setting a memorable one later.</summary>
    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(6);
            var code = new string(random.Select(b => chars[b % chars.Length]).ToArray());
            if (!await _context.Societies.AnyAsync(s => s.Code == code, ct))
            {
                return code;
            }
        }
        throw new InvalidOperationException("Could not generate a unique society code.");
    }
}

// ---- Update ----------------------------------------------------------------
public record UpdateSocietyCommand(
    int Id, string Name, string? RegistrationNumber, string Address, string City, string State, string Pincode,
    string? ContactEmail, string? ContactPhone, DateTime? EstablishedDate, string? LogoUrl = null,
    string? Code = null) : IRequest<Unit>;

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
    private readonly ICurrentUserService _currentUserService;

    public UpdateSocietyCommandHandler(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(UpdateSocietyCommand request, CancellationToken ct)
    {
        var society = await _context.Societies.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Society), request.Id);

        // Society's own Id doubles as "which society" here — SocietyScopeFilter
        // can't catch this (the bound parameter is "Id", not "societyId"), so
        // an Admin editing a society other than their own is checked here.
        // Super Admin (no SocietyId claim) is unrestricted.
        if (_currentUserService.SocietyId.HasValue && _currentUserService.SocietyId != society.Id)
        {
            throw new ForbiddenAccessException("You can only manage your own society.");
        }

        if (!string.IsNullOrWhiteSpace(request.Code) && !string.Equals(request.Code, society.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await _context.Societies.AnyAsync(s => s.Code == request.Code && s.Id != society.Id, ct);
            if (codeTaken)
            {
                throw new ConflictAppException("This society code is already in use.");
            }
            society.Code = request.Code.Trim().ToUpperInvariant();
        }

        society.Name = request.Name;
        society.RegistrationNumber = request.RegistrationNumber;
        society.Address = request.Address;
        society.City = request.City;
        society.State = request.State;
        society.Pincode = request.Pincode;
        society.ContactEmail = request.ContactEmail;
        society.ContactPhone = request.ContactPhone;
        society.EstablishedDate = request.EstablishedDate;
        society.LogoUrl = request.LogoUrl;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Society", nameof(Society),
            society.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Set Subscription ---------------------------------------------------------
// Deliberately separate from UpdateSocietyCommand: that command lets a
// society's own Admin edit their own society (see its inline check above),
// which would let a society extend its own trial. This command inverts that
// check — only the Super Admin (no society_id claim) may call it.
public record SetSocietySubscriptionCommand(int Id, DateTime SubscriptionStartDate, DateTime SubscriptionEndDate) : IRequest<Unit>;

public class SetSocietySubscriptionCommandValidator : AbstractValidator<SetSocietySubscriptionCommand>
{
    public SetSocietySubscriptionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SubscriptionEndDate).GreaterThan(x => x.SubscriptionStartDate);
    }
}

public class SetSocietySubscriptionCommandHandler : IRequestHandler<SetSocietySubscriptionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionCacheInvalidator _cacheInvalidator;

    public SetSocietySubscriptionCommandHandler(IApplicationDbContext context, IAuditService auditService,
        ICurrentUserService currentUserService, ISubscriptionCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Unit> Handle(SetSocietySubscriptionCommand request, CancellationToken ct)
    {
        if (_currentUserService.SocietyId.HasValue)
        {
            throw new ForbiddenAccessException("Only the platform administrator can manage subscriptions.");
        }

        var society = await _context.Societies.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Society), request.Id);

        society.SubscriptionStartDate = request.SubscriptionStartDate;
        society.SubscriptionEndDate = request.SubscriptionEndDate;

        await _context.SaveChangesAsync(ct);
        _cacheInvalidator.Invalidate(society.Id);
        await _auditService.LogAsync(Domain.Enums.AuditAction.Update, "Society", nameof(Society),
            society.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Set Suspension -------------------------------------------------------------
// Manual override, independent of SetSocietySubscriptionCommand's date
// window — lets the Super Admin cut a society off immediately or reinstate
// it without touching dates. Same inverted-check pattern as above.
public record SetSocietySuspensionCommand(int Id, bool IsSuspended) : IRequest<Unit>;

public class SetSocietySuspensionCommandValidator : AbstractValidator<SetSocietySuspensionCommand>
{
    public SetSocietySuspensionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class SetSocietySuspensionCommandHandler : IRequestHandler<SetSocietySuspensionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionCacheInvalidator _cacheInvalidator;

    public SetSocietySuspensionCommandHandler(IApplicationDbContext context, IAuditService auditService,
        ICurrentUserService currentUserService, ISubscriptionCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<Unit> Handle(SetSocietySuspensionCommand request, CancellationToken ct)
    {
        if (_currentUserService.SocietyId.HasValue)
        {
            throw new ForbiddenAccessException("Only the platform administrator can manage subscriptions.");
        }

        var society = await _context.Societies.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Society), request.Id);

        society.IsSubscriptionSuspended = request.IsSuspended;

        await _context.SaveChangesAsync(ct);
        _cacheInvalidator.Invalidate(society.Id);
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
    private readonly ICurrentUserService _currentUserService;

    public DeleteSocietyCommandHandler(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(DeleteSocietyCommand request, CancellationToken ct)
    {
        var society = await _context.Societies
            .Include(s => s.Buildings)
            .FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Society), request.Id);

        if (_currentUserService.SocietyId.HasValue && _currentUserService.SocietyId != society.Id)
        {
            throw new ForbiddenAccessException("You can only manage your own society.");
        }

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
