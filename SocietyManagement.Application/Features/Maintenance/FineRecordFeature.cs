using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Maintenance;

public class FineRecordDto
{
    public int Id { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime FineDate { get; set; }
    public FineStatus Status { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateFineRecordCommand(int FlatId, string Reason, decimal Amount, DateTime FineDate) : IRequest<int>;

public class CreateFineRecordCommandValidator : AbstractValidator<CreateFineRecordCommand>
{
    public CreateFineRecordCommandValidator()
    {
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public record WaiveFineCommand(int Id) : IRequest<Unit>;

public record DeleteFineRecordCommand(int Id) : IRequest<Unit>;

public class FineRecordCommandHandlers :
    IRequestHandler<CreateFineRecordCommand, int>,
    IRequestHandler<WaiveFineCommand, Unit>,
    IRequestHandler<DeleteFineRecordCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FineRecordCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateFineRecordCommand request, CancellationToken ct)
    {
        if (!await _context.Flats.AnyAsync(f => f.Id == request.FlatId && !f.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Flat), request.FlatId);
        }

        var fine = new FineRecord
        {
            FlatId = request.FlatId, Reason = request.Reason, Amount = request.Amount,
            FineDate = request.FineDate, Status = FineStatus.Pending
        };
        await _context.FineRecords.AddAsync(fine, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Maintenance", nameof(FineRecord), fine.Id.ToString(), ct: ct);
        return fine.Id;
    }

    public async Task<Unit> Handle(WaiveFineCommand request, CancellationToken ct)
    {
        var fine = await _context.FineRecords.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FineRecord), request.Id);

        if (fine.Status == FineStatus.Billed)
        {
            throw new ConflictAppException("Cannot waive a fine that has already been billed.");
        }

        fine.Status = FineStatus.Waived;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Maintenance", nameof(FineRecord), fine.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteFineRecordCommand request, CancellationToken ct)
    {
        var fine = await _context.FineRecords.FirstOrDefaultAsync(f => f.Id == request.Id && !f.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FineRecord), request.Id);

        if (fine.Status == FineStatus.Billed)
        {
            throw new ConflictAppException("Cannot delete a fine that has already been billed.");
        }

        fine.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Maintenance", nameof(FineRecord), fine.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetFinesQuery(int SocietyId, int? FlatId, FineStatus? Status) : IRequest<List<FineRecordDto>>;

public class FineRecordQueryHandlers : IRequestHandler<GetFinesQuery, List<FineRecordDto>>
{
    private readonly IApplicationDbContext _context;

    public FineRecordQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<List<FineRecordDto>> Handle(GetFinesQuery request, CancellationToken ct)
    {
        var query = _context.FineRecords
            .Where(f => !f.IsDeleted && f.Flat.Floor.Wing.Building.SocietyId == request.SocietyId);

        if (request.FlatId.HasValue) query = query.Where(f => f.FlatId == request.FlatId);
        if (request.Status.HasValue) query = query.Where(f => f.Status == request.Status);

        return await query
            .OrderByDescending(f => f.FineDate)
            .Select(f => new FineRecordDto
            {
                Id = f.Id, FlatId = f.FlatId, FlatNumber = f.Flat.FlatNumber, Reason = f.Reason,
                Amount = f.Amount, FineDate = f.FineDate, Status = f.Status
            })
            .ToListAsync(ct);
    }
}
