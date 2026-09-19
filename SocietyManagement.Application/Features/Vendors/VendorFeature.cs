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

namespace SocietyManagement.Application.Features.Vendors;

public class VendorDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public ServiceVendorCategory Category { get; set; }
    public string? ContactPerson { get; set; }
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? GstNumber { get; set; }
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public string? PerformanceNotes { get; set; }
    public bool IsActive { get; set; }

    // Sum of general Expense rows whose PaidTo matches this vendor's name —
    // a read-only link into the existing Finance module (Expense has no
    // VendorId; adding one would alter a working table and its commands).
    public decimal TotalPaid { get; set; }
}

public record CreateVendorCommand(
    int SocietyId, string Name, ServiceVendorCategory Category, string? ContactPerson, string Phone, string? Email, string? Address,
    string? GstNumber, DateTime? ContractStart, DateTime? ContractEnd, string? PerformanceNotes) : IRequest<int>;

public class CreateVendorCommandValidator : AbstractValidator<CreateVendorCommand>
{
    public CreateVendorCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.ContractEnd).GreaterThanOrEqualTo(x => x.ContractStart)
            .When(x => x.ContractStart.HasValue && x.ContractEnd.HasValue).WithMessage("Contract end can't be before its start.");
    }
}

public record UpdateVendorCommand(
    int Id, string Name, ServiceVendorCategory Category, string? ContactPerson, string Phone, string? Email, string? Address,
    string? GstNumber, DateTime? ContractStart, DateTime? ContractEnd, string? PerformanceNotes, bool IsActive) : IRequest<Unit>;

public class UpdateVendorCommandValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record DeleteVendorCommand(int Id) : IRequest<Unit>;

// ExpiringWithinDays surfaces vendors whose contract ends soon (renewal
// reminders); null means no such filter.
public record GetVendorsQuery(
    int SocietyId, string? Search, ServiceVendorCategory? Category, bool? IsActive, int? ExpiringWithinDays,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<VendorDto>>;

public class VendorHandlers :
    IRequestHandler<CreateVendorCommand, int>,
    IRequestHandler<UpdateVendorCommand, Unit>,
    IRequestHandler<DeleteVendorCommand, Unit>,
    IRequestHandler<GetVendorsQuery, PaginatedResult<VendorDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public VendorHandlers(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    private async Task<Vendor> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var v = await _context.Vendors.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException(nameof(Vendor), id);
        if (_currentUser.SocietyId.HasValue && _currentUser.SocietyId != v.SocietyId) throw new NotFoundException(nameof(Vendor), id);
        return v;
    }

    public async Task<int> Handle(CreateVendorCommand r, CancellationToken ct)
    {
        var v = new Vendor
        {
            SocietyId = r.SocietyId, Name = r.Name.Trim(), Category = r.Category, ContactPerson = r.ContactPerson, Phone = r.Phone,
            Email = r.Email, Address = r.Address, GstNumber = r.GstNumber, ContractStart = r.ContractStart, ContractEnd = r.ContractEnd,
            PerformanceNotes = r.PerformanceNotes, IsActive = true
        };
        await _context.Vendors.AddAsync(v, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Vendors", nameof(Vendor), v.Id.ToString(), newValues: new { v.Name, v.Category }, ct: ct);
        return v.Id;
    }

    public async Task<Unit> Handle(UpdateVendorCommand r, CancellationToken ct)
    {
        var v = await LoadOwnedAsync(r.Id, ct);
        v.Name = r.Name.Trim(); v.Category = r.Category; v.ContactPerson = r.ContactPerson; v.Phone = r.Phone; v.Email = r.Email;
        v.Address = r.Address; v.GstNumber = r.GstNumber; v.ContractStart = r.ContractStart; v.ContractEnd = r.ContractEnd;
        v.PerformanceNotes = r.PerformanceNotes; v.IsActive = r.IsActive;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Vendors", nameof(Vendor), v.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteVendorCommand r, CancellationToken ct)
    {
        var v = await LoadOwnedAsync(r.Id, ct);
        v.IsDeleted = true;
        v.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Vendors", nameof(Vendor), v.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<PaginatedResult<VendorDto>> Handle(GetVendorsQuery r, CancellationToken ct)
    {
        var query = _context.Vendors.Where(v => v.SocietyId == r.SocietyId);
        if (r.Category.HasValue) query = query.Where(v => v.Category == r.Category);
        if (r.IsActive.HasValue) query = query.Where(v => v.IsActive == r.IsActive);
        if (r.ExpiringWithinDays.HasValue)
        {
            var limit = DateTime.UtcNow.Date.AddDays(r.ExpiringWithinDays.Value);
            query = query.Where(v => v.ContractEnd != null && v.ContractEnd <= limit);
        }
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(term) || v.Phone.Contains(term) ||
                                     (v.ContactPerson != null && v.ContactPerson.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var pageSize = Math.Clamp(r.PageSize, 1, AppConstants.MaxPageSize);
        var page = Math.Max(r.PageNumber, 1);
        var items = await query.OrderBy(v => v.Name).Skip((page - 1) * pageSize).Take(pageSize).Select(v => new VendorDto
        {
            Id = v.Id, SocietyId = v.SocietyId, Name = v.Name, Category = v.Category, ContactPerson = v.ContactPerson, Phone = v.Phone,
            Email = v.Email, Address = v.Address, GstNumber = v.GstNumber, ContractStart = v.ContractStart, ContractEnd = v.ContractEnd,
            PerformanceNotes = v.PerformanceNotes, IsActive = v.IsActive,
            TotalPaid = _context.Expenses
                .Where(e => e.SocietyId == v.SocietyId && e.PaidTo != null && e.PaidTo.ToLower() == v.Name.ToLower())
                .Sum(e => (decimal?)e.Amount) ?? 0m
        }).ToListAsync(ct);
        return new PaginatedResult<VendorDto>(items, total, page, pageSize);
    }
}
