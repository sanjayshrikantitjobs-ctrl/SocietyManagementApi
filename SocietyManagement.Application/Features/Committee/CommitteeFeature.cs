using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Extensions;

namespace SocietyManagement.Application.Features.Committee;

public class CommitteeMemberDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public string Designation { get; set; } = default!;
    public string? FlatNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int DisplayOrder { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateCommitteeMemberCommand(
    int SocietyId, string Name, string Designation, string? FlatNumber, string? Phone, string? Email,
    int DisplayOrder = 0) : IRequest<int>;

public class CreateCommitteeMemberCommandValidator : AbstractValidator<CreateCommitteeMemberCommand>
{
    public CreateCommitteeMemberCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Designation).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record UpdateCommitteeMemberCommand(
    int Id, string Name, string Designation, string? FlatNumber, string? Phone, string? Email,
    int DisplayOrder = 0) : IRequest<Unit>;

public class UpdateCommitteeMemberCommandValidator : AbstractValidator<UpdateCommitteeMemberCommand>
{
    public UpdateCommitteeMemberCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Designation).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record DeleteCommitteeMemberCommand(int Id) : IRequest<Unit>;

public class CommitteeCommandHandlers :
    IRequestHandler<CreateCommitteeMemberCommand, int>,
    IRequestHandler<UpdateCommitteeMemberCommand, Unit>,
    IRequestHandler<DeleteCommitteeMemberCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public CommitteeCommandHandlers(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(CreateCommitteeMemberCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var member = new CommitteeMember
        {
            SocietyId = request.SocietyId, Name = request.Name, Designation = request.Designation,
            FlatNumber = request.FlatNumber, Phone = request.Phone, Email = request.Email,
            DisplayOrder = request.DisplayOrder
        };
        await _context.CommitteeMembers.AddAsync(member, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Committee", nameof(CommitteeMember), member.Id.ToString(), ct: ct);
        return member.Id;
    }

    public async Task<Unit> Handle(UpdateCommitteeMemberCommand request, CancellationToken ct)
    {
        var member = await _context.CommitteeMembers.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CommitteeMember), request.Id);

        // Mutated by its own Id — SocietyScopeFilter can't see a SocietyId
        // argument here, so the ownership check happens after load, same
        // pattern as Society's own Update/Delete handlers.
        if (_currentUserService.SocietyId.HasValue && _currentUserService.SocietyId != member.SocietyId)
        {
            throw new ForbiddenAccessException("You can only manage your own society's committee.");
        }

        member.Name = request.Name;
        member.Designation = request.Designation;
        member.FlatNumber = request.FlatNumber;
        member.Phone = request.Phone;
        member.Email = request.Email;
        member.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Committee", nameof(CommitteeMember), member.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteCommitteeMemberCommand request, CancellationToken ct)
    {
        var member = await _context.CommitteeMembers.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(CommitteeMember), request.Id);

        if (_currentUserService.SocietyId.HasValue && _currentUserService.SocietyId != member.SocietyId)
        {
            throw new ForbiddenAccessException("You can only manage your own society's committee.");
        }

        member.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Committee", nameof(CommitteeMember), member.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetCommitteeMembersQuery(int SocietyId) : IRequest<List<CommitteeMemberDto>>;

public class CommitteeQueryHandlers : IRequestHandler<GetCommitteeMembersQuery, List<CommitteeMemberDto>>
{
    private readonly IApplicationDbContext _context;

    public CommitteeQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<CommitteeMemberDto>> Handle(GetCommitteeMembersQuery request, CancellationToken ct)
    {
        return await _context.CommitteeMembers
            .Where(c => c.SocietyId == request.SocietyId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CommitteeMemberDto
            {
                Id = c.Id, SocietyId = c.SocietyId, Name = c.Name, Designation = c.Designation,
                FlatNumber = c.FlatNumber, Phone = c.Phone, Email = c.Email, DisplayOrder = c.DisplayOrder
            })
            .ToListAsync(ct);
    }
}
