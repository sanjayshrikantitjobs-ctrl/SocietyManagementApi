using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.SocietyDocuments;

public class SocietyDocumentDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public SocietyDocumentCategory Category { get; set; }
    public string FileUrl { get; set; } = default!;
    public string? FileName { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DocumentVisibility Visibility { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record CreateSocietyDocumentCommand(
    int SocietyId, string Title, string? Description, SocietyDocumentCategory Category, string FileUrl, string? FileName,
    DateTime? ExpiryDate, DocumentVisibility Visibility) : IRequest<int>;

public class CreateSocietyDocumentCommandValidator : AbstractValidator<CreateSocietyDocumentCommand>
{
    public CreateSocietyDocumentCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.FileUrl).NotEmpty().MaximumLength(500);
    }
}

public record UpdateSocietyDocumentCommand(
    int Id, string Title, string? Description, SocietyDocumentCategory Category, string FileUrl, string? FileName,
    DateTime? ExpiryDate, DocumentVisibility Visibility) : IRequest<Unit>;

public class UpdateSocietyDocumentCommandValidator : AbstractValidator<UpdateSocietyDocumentCommand>
{
    public UpdateSocietyDocumentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FileUrl).NotEmpty().MaximumLength(500);
    }
}

public record DeleteSocietyDocumentCommand(int Id) : IRequest<Unit>;

// Callers without documents.manage only ever see AllResidents documents,
// whatever the request says — enforced in the handler, not the UI.
public record GetSocietyDocumentsQuery(
    int SocietyId, string? Search, SocietyDocumentCategory? Category, bool ExpiringOnly,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<SocietyDocumentDto>>;

public class SocietyDocumentHandlers :
    IRequestHandler<CreateSocietyDocumentCommand, int>,
    IRequestHandler<UpdateSocietyDocumentCommand, Unit>,
    IRequestHandler<DeleteSocietyDocumentCommand, Unit>,
    IRequestHandler<GetSocietyDocumentsQuery, PaginatedResult<SocietyDocumentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public SocietyDocumentHandlers(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    private async Task<SocietyDocument> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var d = await _context.SocietyDocuments.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(SocietyDocument), id);
        if (_currentUser.SocietyId.HasValue && _currentUser.SocietyId != d.SocietyId) throw new NotFoundException(nameof(SocietyDocument), id);
        return d;
    }

    public async Task<int> Handle(CreateSocietyDocumentCommand r, CancellationToken ct)
    {
        var d = new SocietyDocument
        {
            SocietyId = r.SocietyId, Title = r.Title.Trim(), Description = r.Description, Category = r.Category, FileUrl = r.FileUrl,
            FileName = r.FileName, ExpiryDate = r.ExpiryDate, Visibility = r.Visibility
        };
        await _context.SocietyDocuments.AddAsync(d, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Documents", nameof(SocietyDocument), d.Id.ToString(), newValues: new { d.Title, d.Category }, ct: ct);
        return d.Id;
    }

    public async Task<Unit> Handle(UpdateSocietyDocumentCommand r, CancellationToken ct)
    {
        var d = await LoadOwnedAsync(r.Id, ct);
        d.Title = r.Title.Trim(); d.Description = r.Description; d.Category = r.Category; d.FileUrl = r.FileUrl; d.FileName = r.FileName;
        d.ExpiryDate = r.ExpiryDate; d.Visibility = r.Visibility;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Documents", nameof(SocietyDocument), d.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteSocietyDocumentCommand r, CancellationToken ct)
    {
        var d = await LoadOwnedAsync(r.Id, ct);
        d.IsDeleted = true;
        d.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Documents", nameof(SocietyDocument), d.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<PaginatedResult<SocietyDocumentDto>> Handle(GetSocietyDocumentsQuery r, CancellationToken ct)
    {
        var query = _context.SocietyDocuments.Where(d => d.SocietyId == r.SocietyId);
        if (!_currentUser.HasPermission(SocietyManagement.Shared.Constants.Permissions.Documents.Manage))
            query = query.Where(d => d.Visibility == DocumentVisibility.AllResidents);
        if (r.Category.HasValue) query = query.Where(d => d.Category == r.Category);
        if (r.ExpiringOnly)
        {
            var limit = DateTime.UtcNow.Date.AddDays(30);
            query = query.Where(d => d.ExpiryDate != null && d.ExpiryDate <= limit);
        }
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(term) || (d.Description != null && d.Description.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var pageSize = Math.Clamp(r.PageSize, 1, AppConstants.MaxPageSize);
        var page = Math.Max(r.PageNumber, 1);
        var items = await query.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(d => new SocietyDocumentDto
        {
            Id = d.Id, SocietyId = d.SocietyId, Title = d.Title, Description = d.Description, Category = d.Category, FileUrl = d.FileUrl,
            FileName = d.FileName, ExpiryDate = d.ExpiryDate, Visibility = d.Visibility, CreatedAt = d.CreatedAt
        }).ToListAsync(ct);
        return new PaginatedResult<SocietyDocumentDto>(items, total, page, pageSize);
    }
}
