using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Maintenance;

public class SpecialChargeDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public string ChargeName { get; set; } = default!;
    public decimal Amount { get; set; }
    public ChargeFrequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateSpecialChargeCommand(
    int FlatId, string ChargeName, decimal Amount, ChargeFrequency Frequency,
    DateTime StartDate, DateTime? EndDate, string? Notes) : IRequest<int>;

public class CreateSpecialChargeCommandValidator : AbstractValidator<CreateSpecialChargeCommand>
{
    public CreateSpecialChargeCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.ChargeName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public record UpdateSpecialChargeCommand(
    int Id, string ChargeName, decimal Amount, ChargeFrequency Frequency,
    DateTime StartDate, DateTime? EndDate, string? Notes, bool IsActive) : IRequest<Unit>;

public class UpdateSpecialChargeCommandValidator : AbstractValidator<UpdateSpecialChargeCommand>
{
    public UpdateSpecialChargeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ChargeName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public record DeleteSpecialChargeCommand(int Id) : IRequest<Unit>;

public class SpecialChargeCommandHandlers :
    IRequestHandler<CreateSpecialChargeCommand, int>,
    IRequestHandler<UpdateSpecialChargeCommand, Unit>,
    IRequestHandler<DeleteSpecialChargeCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public SpecialChargeCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateSpecialChargeCommand request, CancellationToken ct)
    {
        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }

        var charge = new SpecialCharge
        {
            FlatId = request.FlatId, ChargeName = request.ChargeName, Amount = request.Amount,
            Frequency = request.Frequency, StartDate = request.StartDate, EndDate = request.EndDate, Notes = request.Notes
        };
        await _context.SpecialCharges.AddAsync(charge, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Maintenance", nameof(SpecialCharge), charge.Id.ToString(), ct: ct);
        return charge.Id;
    }

    public async Task<Unit> Handle(UpdateSpecialChargeCommand request, CancellationToken ct)
    {
        var charge = await _context.SpecialCharges.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(SpecialCharge), request.Id);

        charge.ChargeName = request.ChargeName;
        charge.Amount = request.Amount;
        charge.Frequency = request.Frequency;
        charge.StartDate = request.StartDate;
        charge.EndDate = request.EndDate;
        charge.Notes = request.Notes;
        charge.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Maintenance", nameof(SpecialCharge), charge.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteSpecialChargeCommand request, CancellationToken ct)
    {
        var charge = await _context.SpecialCharges.FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(SpecialCharge), request.Id);

        charge.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Maintenance", nameof(SpecialCharge), charge.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetSpecialChargesQuery(int SocietyId, int? FlatId) : IRequest<List<SpecialChargeDto>>;

public class SpecialChargeQueryHandlers : IRequestHandler<GetSpecialChargesQuery, List<SpecialChargeDto>>
{
    private readonly IApplicationDbContext _context;

    public SpecialChargeQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<SpecialChargeDto>> Handle(GetSpecialChargesQuery request, CancellationToken ct)
    {
        var query = _context.SpecialCharges
            .Where(c => !c.IsDeleted && c.Flat.Floor.Wing.Building.SocietyId == request.SocietyId);

        if (request.FlatId.HasValue) query = query.Where(c => c.FlatId == request.FlatId);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new SpecialChargeDto
            {
                Id = c.Id, FlatId = c.FlatId, FlatNumber = c.Flat.FlatNumber, ChargeName = c.ChargeName,
                Amount = c.Amount, Frequency = c.Frequency, StartDate = c.StartDate, EndDate = c.EndDate,
                Notes = c.Notes, IsActive = c.IsActive
            })
            .ToListAsync(ct);
    }
}
