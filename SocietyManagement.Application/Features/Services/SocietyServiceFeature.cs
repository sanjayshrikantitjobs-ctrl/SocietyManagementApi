using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Services;

public class SocietyServiceDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string ServiceName { get; set; } = default!;
    public string VendorName { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public string ContactNumber { get; set; } = default!;
    public string? Email { get; set; }
    public DateTime RenewalDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class ExpiringServiceDto
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = default!;
    public string VendorName { get; set; } = default!;
    public DateTime RenewalDate { get; set; }
    public int DaysRemaining { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateSocietyServiceCommand(
    int SocietyId, string ServiceName, string VendorName, string? ContactPerson, string ContactNumber,
    string? Email, DateTime RenewalDate, string? Notes) : IRequest<int>;

public class CreateSocietyServiceCommandValidator : AbstractValidator<CreateSocietyServiceCommand>
{
    public CreateSocietyServiceCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.ServiceName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ContactNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record UpdateSocietyServiceCommand(
    int Id, string ServiceName, string VendorName, string? ContactPerson, string ContactNumber,
    string? Email, DateTime RenewalDate, string? Notes, bool IsActive) : IRequest<Unit>;

public class UpdateSocietyServiceCommandValidator : AbstractValidator<UpdateSocietyServiceCommand>
{
    public UpdateSocietyServiceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ServiceName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ContactNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record DeleteSocietyServiceCommand(int Id) : IRequest<Unit>;

public class SocietyServiceCommandHandlers :
    IRequestHandler<CreateSocietyServiceCommand, int>,
    IRequestHandler<UpdateSocietyServiceCommand, Unit>,
    IRequestHandler<DeleteSocietyServiceCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public SocietyServiceCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateSocietyServiceCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var service = new SocietyService
        {
            SocietyId = request.SocietyId, ServiceName = request.ServiceName, VendorName = request.VendorName,
            ContactPerson = request.ContactPerson, ContactNumber = request.ContactNumber, Email = request.Email,
            RenewalDate = request.RenewalDate, Notes = request.Notes, IsActive = true
        };
        await _context.SocietyServices.AddAsync(service, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Services", nameof(SocietyService), service.Id.ToString(), ct: ct);
        return service.Id;
    }

    public async Task<Unit> Handle(UpdateSocietyServiceCommand request, CancellationToken ct)
    {
        var service = await _context.SocietyServices.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(SocietyService), request.Id);

        service.ServiceName = request.ServiceName;
        service.VendorName = request.VendorName;
        service.ContactPerson = request.ContactPerson;
        service.ContactNumber = request.ContactNumber;
        service.Email = request.Email;
        service.RenewalDate = request.RenewalDate;
        service.Notes = request.Notes;
        service.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Services", nameof(SocietyService), service.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteSocietyServiceCommand request, CancellationToken ct)
    {
        var service = await _context.SocietyServices.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(SocietyService), request.Id);
        service.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Services", nameof(SocietyService), service.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetSocietyServicesQuery(
    int SocietyId, string? Search, bool? IsActive, string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<SocietyServiceDto>>;

public record GetSocietyServiceByIdQuery(int Id) : IRequest<SocietyServiceDto>;

/// <summary>Backs the topbar notification bell — every service whose
/// RenewalDate is within 10 days (including already-overdue ones, so
/// nothing silently disappears from the count).</summary>
public record GetExpiringServicesQuery(int SocietyId, int WithinDays = 10) : IRequest<List<ExpiringServiceDto>>;

public class SocietyServiceQueryHandlers :
    IRequestHandler<GetSocietyServicesQuery, PaginatedResult<SocietyServiceDto>>,
    IRequestHandler<GetSocietyServiceByIdQuery, SocietyServiceDto>,
    IRequestHandler<GetExpiringServicesQuery, List<ExpiringServiceDto>>
{
    private readonly IApplicationDbContext _context;

    public SocietyServiceQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedResult<SocietyServiceDto>> Handle(GetSocietyServicesQuery request, CancellationToken ct)
    {
        var query = _context.SocietyServices.Where(s => s.SocietyId == request.SocietyId && !s.IsDeleted);

        if (request.IsActive.HasValue) query = query.Where(s => s.IsActive == request.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s => s.ServiceName.ToLower().Contains(term) || s.VendorName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("servicename", true) => query.OrderByDescending(s => s.ServiceName),
            ("vendorname", false) => query.OrderBy(s => s.VendorName),
            ("vendorname", true) => query.OrderByDescending(s => s.VendorName),
            ("renewaldate", true) => query.OrderByDescending(s => s.RenewalDate),
            _ => query.OrderBy(s => s.RenewalDate)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SocietyServiceDto
            {
                Id = s.Id, SocietyId = s.SocietyId, ServiceName = s.ServiceName, VendorName = s.VendorName,
                ContactPerson = s.ContactPerson, ContactNumber = s.ContactNumber, Email = s.Email,
                RenewalDate = s.RenewalDate, Notes = s.Notes, IsActive = s.IsActive
            })
            .ToListAsync(ct);

        return new PaginatedResult<SocietyServiceDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<SocietyServiceDto> Handle(GetSocietyServiceByIdQuery request, CancellationToken ct)
    {
        var service = await _context.SocietyServices.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(SocietyService), request.Id);

        return new SocietyServiceDto
        {
            Id = service.Id, SocietyId = service.SocietyId, ServiceName = service.ServiceName, VendorName = service.VendorName,
            ContactPerson = service.ContactPerson, ContactNumber = service.ContactNumber, Email = service.Email,
            RenewalDate = service.RenewalDate, Notes = service.Notes, IsActive = service.IsActive
        };
    }

    public async Task<List<ExpiringServiceDto>> Handle(GetExpiringServicesQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(request.WithinDays);

        var services = await _context.SocietyServices
            .Where(s => s.SocietyId == request.SocietyId && !s.IsDeleted && s.IsActive && s.RenewalDate <= cutoff)
            .OrderBy(s => s.RenewalDate)
            .Select(s => new { s.Id, s.ServiceName, s.VendorName, s.RenewalDate })
            .ToListAsync(ct);

        return services
            .Select(s => new ExpiringServiceDto
            {
                Id = s.Id, ServiceName = s.ServiceName, VendorName = s.VendorName, RenewalDate = s.RenewalDate,
                DaysRemaining = (s.RenewalDate.Date - today).Days
            })
            .ToList();
    }
}
