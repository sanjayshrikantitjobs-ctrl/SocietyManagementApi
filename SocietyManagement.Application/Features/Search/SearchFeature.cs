using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Features.Search;

public class SearchResultDto
{
    public string Type { get; set; } = default!;
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Subtitle { get; set; }
    // Angular route the result opens.
    public string Route { get; set; } = default!;
}

public class OperationsAttentionDto
{
    public int? VendorContractsExpiring { get; set; }
    public int? LowStockItems { get; set; }
    public int? PurchasesPendingApproval { get; set; }
    public int? DocumentsExpiring { get; set; }
    public int? PetVaccinationsOverdue { get; set; }
    public int? StaffNotMarkedToday { get; set; }
}

public record GlobalSearchQuery(int SocietyId, string Term) : IRequest<List<SearchResultDto>>;

public record GetOperationsAttentionQuery(int SocietyId) : IRequest<OperationsAttentionDto>;

public class SearchHandlers :
    IRequestHandler<GlobalSearchQuery, List<SearchResultDto>>,
    IRequestHandler<GetOperationsAttentionQuery, OperationsAttentionDto>
{
    private const int PerGroup = 5;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SearchHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Every source is included only when the caller holds that module's own
    // permission, so search never reveals more than the module pages do.
    public async Task<List<SearchResultDto>> Handle(GlobalSearchQuery r, CancellationToken ct)
    {
        var term = (r.Term ?? string.Empty).Trim().ToLower();
        var results = new List<SearchResultDto>();
        if (term.Length < 2) return results;
        var societyId = r.SocietyId;

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Occupancy.Manage))
        {
            var flats = await _context.Flats
                .Where(f => f.Floor.Wing.Building.SocietyId == societyId &&
                            (f.FlatNumber.ToLower().Contains(term) || (f.OwnerName != null && f.OwnerName.ToLower().Contains(term))))
                .OrderBy(f => f.FlatNumber).Take(PerGroup)
                .Select(f => new SearchResultDto { Type = "Flat", Id = f.Id, Title = "Flat " + f.FlatNumber, Subtitle = f.OwnerName, Route = "/residents" })
                .ToListAsync(ct);
            results.AddRange(flats);
        }

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Vehicles.Search))
        {
            var vehicles = await _context.Vehicles
                .Where(v => v.Flat != null && v.Flat.Floor.Wing.Building.SocietyId == societyId && v.RegistrationNumber.ToLower().Contains(term))
                .Take(PerGroup)
                .Select(v => new SearchResultDto
                {
                    Type = "Vehicle", Id = v.Id, Title = v.RegistrationNumber, Subtitle = "Flat " + v.Flat!.FlatNumber, Route = "/vehicle-security"
                }).ToListAsync(ct);
            results.AddRange(vehicles);
        }

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Staff.View))
        {
            var staff = await _context.Staff
                .Where(s => s.SocietyId == societyId && (s.FirstName.ToLower().Contains(term) || s.LastName.ToLower().Contains(term) || s.Phone.Contains(term)))
                .Take(PerGroup)
                .Select(s => new SearchResultDto { Type = "Staff", Id = s.Id, Title = s.FirstName + " " + s.LastName, Subtitle = s.Phone, Route = "/staff/" + s.Id })
                .ToListAsync(ct);
            results.AddRange(staff);
        }

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Vendors.View))
        {
            var vendors = await _context.Vendors
                .Where(v => v.SocietyId == societyId && (v.Name.ToLower().Contains(term) || v.Phone.Contains(term)))
                .Take(PerGroup)
                .Select(v => new SearchResultDto { Type = "Vendor", Id = v.Id, Title = v.Name, Subtitle = v.Phone, Route = "/vendors" })
                .ToListAsync(ct);
            results.AddRange(vendors);
        }

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Documents.View))
        {
            var docs = _context.SocietyDocuments.Where(d => d.SocietyId == societyId && d.Title.ToLower().Contains(term));
            if (!_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Documents.Manage))
                docs = docs.Where(d => d.Visibility == DocumentVisibility.AllResidents);
            results.AddRange(await docs.Take(PerGroup)
                .Select(d => new SearchResultDto { Type = "Document", Id = d.Id, Title = d.Title, Subtitle = null, Route = "/documents" })
                .ToListAsync(ct));
        }

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Complaints.Manage))
        {
            var complaints = await _context.Complaints
                .Where(c => c.SocietyId == societyId && (c.Title.ToLower().Contains(term) || c.RaisedByName.ToLower().Contains(term)))
                .OrderByDescending(c => c.CreatedAt).Take(PerGroup)
                .Select(c => new SearchResultDto { Type = "Complaint", Id = c.Id, Title = c.Title, Subtitle = c.RaisedByName, Route = "/complaints" })
                .ToListAsync(ct);
            results.AddRange(complaints);
        }

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Pets.View))
        {
            var pets = await _context.Pets
                .Where(p => p.SocietyId == societyId && (p.Name.ToLower().Contains(term) || (p.Breed != null && p.Breed.ToLower().Contains(term))))
                .Take(PerGroup)
                .Select(p => new SearchResultDto { Type = "Pet", Id = p.Id, Title = p.Name, Subtitle = "Flat " + p.Flat.FlatNumber, Route = "/pets" })
                .ToListAsync(ct);
            results.AddRange(pets);
        }

        return results;
    }

    public async Task<OperationsAttentionDto> Handle(GetOperationsAttentionQuery r, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var soon = today.AddDays(30);
        var dto = new OperationsAttentionDto();

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Vendors.View))
            dto.VendorContractsExpiring = await _context.Vendors.CountAsync(
                v => v.SocietyId == r.SocietyId && v.IsActive && v.ContractEnd != null && v.ContractEnd <= soon, ct);

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Inventory.View))
            dto.LowStockItems = await _context.InventoryItems.CountAsync(i =>
                i.SocietyId == r.SocietyId && i.IsActive && i.MinimumStock > 0 &&
                (_context.StockTransactions.Where(t => t.InventoryItemId == i.Id).Sum(t => (decimal?)t.Quantity) ?? 0m) <= i.MinimumStock, ct);

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Purchases.Approve))
            dto.PurchasesPendingApproval = await _context.PurchaseRequests.CountAsync(
                p => p.SocietyId == r.SocietyId && p.Status == PurchaseStatus.PendingApproval, ct);

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Documents.Manage))
            dto.DocumentsExpiring = await _context.SocietyDocuments.CountAsync(
                d => d.SocietyId == r.SocietyId && d.ExpiryDate != null && d.ExpiryDate <= soon, ct);

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Pets.View))
            dto.PetVaccinationsOverdue = await _context.Pets.CountAsync(
                p => p.SocietyId == r.SocietyId && p.IsActive && p.NextVaccinationDue != null && p.NextVaccinationDue < today, ct);

        if (_currentUser.HasPermission(global::SocietyManagement.Shared.Constants.Permissions.Staff.View))
        {
            var activeStaff = await _context.Staff.CountAsync(s => s.SocietyId == r.SocietyId && s.IsActive, ct);
            var marked = await _context.StaffAttendances.CountAsync(a => a.SocietyId == r.SocietyId && a.Date == today, ct);
            dto.StaffNotMarkedToday = Math.Max(activeStaff - marked, 0);
        }

        return dto;
    }
}
