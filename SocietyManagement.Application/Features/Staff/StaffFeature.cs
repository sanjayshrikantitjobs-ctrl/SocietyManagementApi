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

namespace SocietyManagement.Application.Features.Staff;

public class StaffDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public StaffCategory Category { get; set; }
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTime JoiningDate { get; set; }
    public string? JoiningDocumentUrl { get; set; }
    public string? PhotoUrl { get; set; }
    public decimal Salary { get; set; }
    public int SalaryPayDay { get; set; }
    public bool IsActive { get; set; }
}

// ---- Commands ----------------------------------------------------------------
public record CreateStaffCommand(
    int SocietyId, string FirstName, string LastName, StaffCategory Category, string Phone, string? Email,
    string? Address, DateTime JoiningDate, string? JoiningDocumentUrl, string? PhotoUrl,
    decimal Salary, int SalaryPayDay) : IRequest<int>;

public class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalaryPayDay).InclusiveBetween(1, 31);
    }
}

public record UpdateStaffCommand(
    int Id, string FirstName, string LastName, StaffCategory Category, string Phone, string? Email,
    string? Address, DateTime JoiningDate, string? JoiningDocumentUrl, string? PhotoUrl,
    decimal Salary, int SalaryPayDay, bool IsActive) : IRequest<Unit>;

public class UpdateStaffCommandValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().Must(p => p.IsValidIndianMobile()).WithMessage("A valid 10-digit mobile number is required.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalaryPayDay).InclusiveBetween(1, 31);
    }
}

public record DeleteStaffCommand(int Id) : IRequest<Unit>;

public class StaffCommandHandlers :
    IRequestHandler<CreateStaffCommand, int>,
    IRequestHandler<UpdateStaffCommand, Unit>,
    IRequestHandler<DeleteStaffCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public StaffCommandHandlers(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<int> Handle(CreateStaffCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var staff = new Domain.Entities.Staff
        {
            SocietyId = request.SocietyId, FirstName = request.FirstName, LastName = request.LastName,
            Category = request.Category, Phone = request.Phone, Email = request.Email, Address = request.Address,
            JoiningDate = request.JoiningDate, JoiningDocumentUrl = request.JoiningDocumentUrl, PhotoUrl = request.PhotoUrl,
            Salary = request.Salary, SalaryPayDay = request.SalaryPayDay, IsActive = true
        };
        await _context.Staff.AddAsync(staff, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Staff", nameof(Domain.Entities.Staff), staff.Id.ToString(), ct: ct);
        return staff.Id;
    }

    public async Task<Unit> Handle(UpdateStaffCommand request, CancellationToken ct)
    {
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Staff), request.Id);

        staff.FirstName = request.FirstName;
        staff.LastName = request.LastName;
        staff.Category = request.Category;
        staff.Phone = request.Phone;
        staff.Email = request.Email;
        staff.Address = request.Address;
        staff.JoiningDate = request.JoiningDate;
        staff.JoiningDocumentUrl = request.JoiningDocumentUrl;
        staff.PhotoUrl = request.PhotoUrl;
        staff.Salary = request.Salary;
        staff.SalaryPayDay = request.SalaryPayDay;
        staff.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Staff", nameof(Domain.Entities.Staff), staff.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeleteStaffCommand request, CancellationToken ct)
    {
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Staff), request.Id);
        staff.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Staff", nameof(Domain.Entities.Staff), staff.Id.ToString(), ct: ct);
        return Unit.Value;
    }
}

// ---- Queries -------------------------------------------------------------------
public record GetStaffQuery(
    int SocietyId, string? Search, StaffCategory? Category, bool? IsActive, string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<StaffDto>>;

public record GetStaffByIdQuery(int Id) : IRequest<StaffDto>;

public class StaffQueryHandlers :
    IRequestHandler<GetStaffQuery, PaginatedResult<StaffDto>>,
    IRequestHandler<GetStaffByIdQuery, StaffDto>
{
    private readonly IApplicationDbContext _context;

    public StaffQueryHandlers(IApplicationDbContext context) => _context = context;

    public async Task<PaginatedResult<StaffDto>> Handle(GetStaffQuery request, CancellationToken ct)
    {
        var query = _context.Staff.Where(s => s.SocietyId == request.SocietyId && !s.IsDeleted);

        if (request.Category.HasValue) query = query.Where(s => s.Category == request.Category);
        if (request.IsActive.HasValue) query = query.Where(s => s.IsActive == request.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s => s.FirstName.ToLower().Contains(term) || s.LastName.ToLower().Contains(term)
                || s.Phone.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("name", true) => query.OrderByDescending(s => s.FirstName).ThenByDescending(s => s.LastName),
            ("category", false) => query.OrderBy(s => s.Category),
            ("category", true) => query.OrderByDescending(s => s.Category),
            ("joiningdate", false) => query.OrderBy(s => s.JoiningDate),
            ("joiningdate", true) => query.OrderByDescending(s => s.JoiningDate),
            _ => query.OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StaffDto
            {
                Id = s.Id, SocietyId = s.SocietyId, FirstName = s.FirstName, LastName = s.LastName,
                Category = s.Category, Phone = s.Phone, Email = s.Email, Address = s.Address,
                JoiningDate = s.JoiningDate, JoiningDocumentUrl = s.JoiningDocumentUrl, PhotoUrl = s.PhotoUrl,
                Salary = s.Salary, SalaryPayDay = s.SalaryPayDay, IsActive = s.IsActive
            })
            .ToListAsync(ct);

        return new PaginatedResult<StaffDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<StaffDto> Handle(GetStaffByIdQuery request, CancellationToken ct)
    {
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.Id == request.Id && !s.IsDeleted, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Staff), request.Id);

        return new StaffDto
        {
            Id = staff.Id, SocietyId = staff.SocietyId, FirstName = staff.FirstName, LastName = staff.LastName,
            Category = staff.Category, Phone = staff.Phone, Email = staff.Email, Address = staff.Address,
            JoiningDate = staff.JoiningDate, JoiningDocumentUrl = staff.JoiningDocumentUrl, PhotoUrl = staff.PhotoUrl,
            Salary = staff.Salary, SalaryPayDay = staff.SalaryPayDay, IsActive = staff.IsActive
        };
    }
}
