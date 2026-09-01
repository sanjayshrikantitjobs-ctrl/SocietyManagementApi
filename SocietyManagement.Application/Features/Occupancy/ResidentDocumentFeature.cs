using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Occupancy;

// ---- DTOs ----------------------------------------------------------------------

public class ResidentDocumentDto
{
    public int Id { get; set; }
    public int FlatOccupancyId { get; set; }
    public ResidentDocumentType DocumentType { get; set; }
    public string DocumentUrl { get; set; } = default!;
    public string? Notes { get; set; }
    public string UploadedByName { get; set; } = default!;
    public DateTime UploadedAt { get; set; }
}

// ---- Commands --------------------------------------------------------------------

/// <summary>Takes an already-uploaded URL — the desktop admin-form flow
/// uploads via the generic POST /api/files/upload first (same two-step
/// RentalAgreementCard already uses), then this just persists the pointer.</summary>
public record UploadResidentDocumentCommand(
    int FlatOccupancyId, ResidentDocumentType DocumentType, string DocumentUrl, string? Notes) : IRequest<int>;

public class UploadResidentDocumentCommandValidator : AbstractValidator<UploadResidentDocumentCommand>
{
    public UploadResidentDocumentCommandValidator()
    {
        RuleFor(x => x.FlatOccupancyId).GreaterThan(0);
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.DocumentUrl).NotEmpty().MaximumLength(500);
    }
}

public record DeleteResidentDocumentCommand(int Id) : IRequest<Unit>;

// ---- Queries -------------------------------------------------------------------

public record GetResidentDocumentsQuery(int FlatOccupancyId) : IRequest<List<ResidentDocumentDto>>;

// ---- Handlers --------------------------------------------------------------------

public class ResidentDocumentHandlers :
    IRequestHandler<UploadResidentDocumentCommand, int>,
    IRequestHandler<DeleteResidentDocumentCommand, Unit>,
    IRequestHandler<GetResidentDocumentsQuery, List<ResidentDocumentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly IAuditService _auditService;

    public ResidentDocumentHandlers(
        IApplicationDbContext context, ICurrentUserService currentUserService, IDateTime dateTime, IAuditService auditService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _auditService = auditService;
    }

    public async Task<int> Handle(UploadResidentDocumentCommand request, CancellationToken ct)
    {
        var exists = await _context.FlatOccupancies.AnyAsync(o => o.Id == request.FlatOccupancyId && !o.IsDeleted, ct);
        if (!exists)
        {
            throw new NotFoundException(nameof(FlatOccupancy), request.FlatOccupancyId);
        }

        var document = new ResidentDocument
        {
            FlatOccupancyId = request.FlatOccupancyId,
            DocumentType = request.DocumentType,
            DocumentUrl = request.DocumentUrl,
            Notes = request.Notes,
            UploadedByUserId = _currentUserService.UserId!.Value,
            UploadedAt = _dateTime.UtcNow
        };
        await _context.ResidentDocuments.AddAsync(document, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Occupancy", nameof(ResidentDocument), document.Id.ToString(), ct: ct);

        return document.Id;
    }

    public async Task<Unit> Handle(DeleteResidentDocumentCommand request, CancellationToken ct)
    {
        var document = await _context.ResidentDocuments.FirstOrDefaultAsync(d => d.Id == request.Id && !d.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(ResidentDocument), request.Id);

        document.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Occupancy", nameof(ResidentDocument), document.Id.ToString(), ct: ct);

        return Unit.Value;
    }

    public async Task<List<ResidentDocumentDto>> Handle(GetResidentDocumentsQuery request, CancellationToken ct) =>
        await _context.ResidentDocuments
            .Where(d => d.FlatOccupancyId == request.FlatOccupancyId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new ResidentDocumentDto
            {
                Id = d.Id,
                FlatOccupancyId = d.FlatOccupancyId,
                DocumentType = d.DocumentType,
                DocumentUrl = d.DocumentUrl,
                Notes = d.Notes,
                UploadedByName = d.UploadedByUser.FirstName + " " + d.UploadedByUser.LastName,
                UploadedAt = d.UploadedAt
            })
            .ToListAsync(ct);
}
