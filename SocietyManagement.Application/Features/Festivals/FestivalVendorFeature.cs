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

namespace SocietyManagement.Application.Features.Festivals;

public class FestivalVendorDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string Name { get; set; } = default!;
    public VendorCategory Category { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
    public decimal Rating { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int PreviousFestivalsCount { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateVendorCommand(
    int SocietyId, string Name, VendorCategory Category, string? Phone, string? Email,
    string? GstNumber, string? Address, decimal Rating) : IRequest<int>;

public class CreateVendorCommandValidator : AbstractValidator<CreateVendorCommand>
{
    public CreateVendorCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Rating).InclusiveBetween(0, 5);
    }
}

public record UpdateVendorCommand(
    int Id, string Name, VendorCategory Category, string? Phone, string? Email,
    string? GstNumber, string? Address, decimal Rating) : IRequest<Unit>;

public class UpdateVendorCommandValidator : AbstractValidator<UpdateVendorCommand>
{
    public UpdateVendorCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).Must(p => p!.IsValidIndianMobile()).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Rating).InclusiveBetween(0, 5);
    }
}

public record DeleteVendorCommand(int Id) : IRequest<Unit>;

public class FestivalVendorCommandHandlers :
    IRequestHandler<CreateVendorCommand, int>,
    IRequestHandler<UpdateVendorCommand, Unit>,
    IRequestHandler<DeleteVendorCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FestivalVendorCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateVendorCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var vendor = new FestivalVendor
        {
            SocietyId = request.SocietyId,
            Name = request.Name,
            Category = request.Category,
            Phone = request.Phone,
            Email = request.Email,
            GstNumber = request.GstNumber,
            Address = request.Address,
            Rating = request.Rating
        };
        await _context.FestivalVendors.AddAsync(vendor, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Festivals", nameof(FestivalVendor), vendor.Id.ToString(), ct: ct);
        return vendor.Id;
    }

    public async Task<Unit> Handle(UpdateVendorCommand request, CancellationToken ct)
    {
        var vendor = await _context.FestivalVendors.FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalVendor), request.Id);

        vendor.Name = request.Name;
        vendor.Category = request.Category;
        vendor.Phone = request.Phone;
        vendor.Email = request.Email;
        vendor.GstNumber = request.GstNumber;
        vendor.Address = request.Address;
        vendor.Rating = request.Rating;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Festivals", nameof(FestivalVendor), vendor.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteVendorCommand request, CancellationToken ct)
    {
        var vendor = await _context.FestivalVendors.Include(v => v.Expenses)
            .FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(FestivalVendor), request.Id);

        if (vendor.Expenses.Any(e => !e.IsDeleted))
        {
            throw new ConflictAppException("Cannot delete a vendor that has expenses recorded against it.");
        }

        vendor.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Festivals", nameof(FestivalVendor), vendor.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetVendorsQuery(
    int SocietyId, VendorCategory? Category, string? Search,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<FestivalVendorDto>>;

public class FestivalVendorQueryHandlers : IRequestHandler<GetVendorsQuery, PaginatedResult<FestivalVendorDto>>
{
    private readonly IApplicationDbContext _context;

    public FestivalVendorQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedResult<FestivalVendorDto>> Handle(GetVendorsQuery request, CancellationToken ct)
    {
        var query = _context.FestivalVendors.Where(v => v.SocietyId == request.SocietyId);

        if (request.Category.HasValue) query = query.Where(v => v.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderBy(v => v.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new FestivalVendorDto
            {
                Id = v.Id,
                SocietyId = v.SocietyId,
                Name = v.Name,
                Category = v.Category,
                Phone = v.Phone,
                Email = v.Email,
                GstNumber = v.GstNumber,
                Address = v.Address,
                Rating = v.Rating,
                TotalPayments = v.Expenses.Where(e => e.ApprovalStatus == ExpenseApprovalStatus.Paid).Sum(e => (decimal?)e.Amount) ?? 0,
                OutstandingAmount = v.Expenses.Where(e => e.ApprovalStatus == ExpenseApprovalStatus.Approved).Sum(e => (decimal?)e.Amount) ?? 0,
                PreviousFestivalsCount = v.Expenses.Select(e => e.FestivalId).Distinct().Count()
            })
            .ToListAsync(ct);

        return new PaginatedResult<FestivalVendorDto>(items, totalCount, pageNumber, pageSize);
    }
}
