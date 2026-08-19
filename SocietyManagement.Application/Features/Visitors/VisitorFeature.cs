using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Visitors;

public class VisitorDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public string MobileNumber { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public string? VehicleNumber { get; set; }
    public string? VehicleType { get; set; }
    public string? IdType { get; set; }
    public string? IdReference { get; set; }
    public string? Notes { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateVisitorCommand(
    int SocietyId, string Name, string MobileNumber, string? PhotoUrl, string? VehicleNumber,
    string? VehicleType, string? IdType, string? IdReference, string? Notes) : IRequest<int>;

public class CreateVisitorCommandValidator : AbstractValidator<CreateVisitorCommand>
{
    public CreateVisitorCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MobileNumber).NotEmpty().MaximumLength(20);
    }
}

public class VisitorCommandHandlers : IRequestHandler<CreateVisitorCommand, int>
{
    private readonly IApplicationDbContext _context;

    public VisitorCommandHandlers(IApplicationDbContext context) => _context = context;

    public async Task<int> Handle(CreateVisitorCommand request, CancellationToken ct)
    {
        var visitor = new Visitor
        {
            SocietyId = request.SocietyId, Name = request.Name, MobileNumber = request.MobileNumber,
            PhotoUrl = request.PhotoUrl, VehicleNumber = request.VehicleNumber, VehicleType = request.VehicleType,
            IdType = request.IdType, IdReference = request.IdReference, Notes = request.Notes
        };
        await _context.Visitors.AddAsync(visitor, ct);
        await _context.SaveChangesAsync(ct);
        return visitor.Id;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetVisitorsQuery(
    int SocietyId, string? Search, int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize)
    : IRequest<PaginatedResult<VisitorDto>>;

public class VisitorQueryHandlers : IRequestHandler<GetVisitorsQuery, PaginatedResult<VisitorDto>>
{
    private readonly IApplicationDbContext _context;

    public VisitorQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedResult<VisitorDto>> Handle(GetVisitorsQuery request, CancellationToken ct)
    {
        var query = _context.Visitors.Where(v => v.SocietyId == request.SocietyId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(term) || v.MobileNumber.Contains(term)
                || (v.VehicleNumber != null && v.VehicleNumber.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderBy(v => v.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VisitorDto
            {
                Id = v.Id, SocietyId = v.SocietyId, Name = v.Name, MobileNumber = v.MobileNumber, PhotoUrl = v.PhotoUrl,
                VehicleNumber = v.VehicleNumber, VehicleType = v.VehicleType, IdType = v.IdType,
                IdReference = v.IdReference, Notes = v.Notes
            })
            .ToListAsync(ct);

        return new PaginatedResult<VisitorDto>(items, totalCount, pageNumber, pageSize);
    }
}
