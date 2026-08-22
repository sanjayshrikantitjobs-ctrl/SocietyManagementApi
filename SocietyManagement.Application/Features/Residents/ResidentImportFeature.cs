using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Residents;

/// <summary>One parsed spreadsheet row, before any DB validation — just the
/// raw cell values read off row N (row 1 is the header).</summary>
public class ResidentImportRowDto
{
    public int RowNumber { get; set; }
    public string? Building { get; set; }
    public string? Wing { get; set; }
    public int FloorNumber { get; set; }
    public string? FlatNumber { get; set; }
    public string? FlatTypeLabel { get; set; }
    public decimal? AreaSqFt { get; set; }
    public bool IsVacant { get; set; }
    public string? OwnerFirstName { get; set; }
    public string? OwnerLastName { get; set; }
    public string? OwnerPhone { get; set; }
    public string? OwnerEmail { get; set; }
    public string? MemberTypeLabel { get; set; }
    public DateTime? MoveInDate { get; set; }
}

public class ResidentImportRowResultDto
{
    public int RowNumber { get; set; }
    public string Status { get; set; } = default!; // Created | Updated | Skipped | Error
    public string Message { get; set; } = default!;
}

public class ResidentImportResultDto
{
    public int TotalRows { get; set; }
    public int FlatsCreated { get; set; }
    public int MembersCreated { get; set; }
    public int ResidenciesCreated { get; set; }
    public int RowsFailed { get; set; }
    public List<ResidentImportRowResultDto> RowResults { get; set; } = new();
}

// ---- Command -------------------------------------------------------------------
public record ImportResidentsCommand(int SocietyId, byte[] FileContent) : IRequest<ResidentImportResultDto>;

public class ImportResidentsCommandValidator : AbstractValidator<ImportResidentsCommand>
{
    public ImportResidentsCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.FileContent).NotEmpty().WithMessage("The uploaded file is empty.");
    }
}

/// <summary>Best-effort, row-by-row bulk import: Building/Wing/Floor/Flat are
/// resolved-or-created by name so a brand-new society's whole hierarchy can
/// come from one spreadsheet, same idea as QuickAddFlatDialogComponent's
/// "add if missing" inline-add buttons but scripted across many rows.
/// A bad row is recorded as an error and the rest of the file still
/// processes — a spreadsheet with a few typos shouldn't block everything
/// else from importing. Re-running the same file is safe: flats are matched
/// by (Floor, FlatNumber), members by (Society, Phone), and a residency is
/// only added if the flat doesn't already have a current one for that
/// member — no duplicates on a second upload.</summary>
public class ImportResidentsCommandHandler : IRequestHandler<ImportResidentsCommand, ResidentImportResultDto>
{
    private static readonly Dictionary<string, FlatType> FlatTypeLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1RK"] = FlatType.OneRK, ["1BHK"] = FlatType.OneBHK, ["2BHK"] = FlatType.TwoBHK,
        ["3BHK"] = FlatType.ThreeBHK, ["4BHK"] = FlatType.FourBHK, ["DUPLEX"] = FlatType.Duplex,
        ["PENTHOUSE"] = FlatType.Penthouse, ["SHOP"] = FlatType.Shop, ["OFFICE"] = FlatType.Office
    };

    private static readonly Dictionary<string, MemberType> MemberTypeLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OWNER"] = MemberType.Owner, ["TENANT"] = MemberType.Tenant
    };

    private readonly IApplicationDbContext _context;
    private readonly IResidentImportService _importService;
    private readonly IAuditService _auditService;

    public ImportResidentsCommandHandler(
        IApplicationDbContext context, IResidentImportService importService, IAuditService auditService)
    {
        _context = context;
        _importService = importService;
        _auditService = auditService;
    }

    public async Task<ResidentImportResultDto> Handle(ImportResidentsCommand request, CancellationToken ct)
    {
        if (!await _context.Societies.AnyAsync(s => s.Id == request.SocietyId && !s.IsDeleted, ct))
        {
            throw new NotFoundException(nameof(Society), request.SocietyId);
        }

        var rows = _importService.ParseRows(request.FileContent);
        var result = new ResidentImportResultDto { TotalRows = rows.Count };

        foreach (var row in rows)
        {
            await ProcessRowAsync(request.SocietyId, row, result, ct);
        }

        await _auditService.LogAsync(AuditAction.Create, "Residents", "ResidentImport", request.SocietyId.ToString(),
            newValues: new
            {
                result.TotalRows, result.FlatsCreated, result.MembersCreated,
                result.ResidenciesCreated, result.RowsFailed
            }, ct: ct);

        return result;
    }

    private async Task ProcessRowAsync(int societyId, ResidentImportRowDto row, ResidentImportResultDto result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(row.Building) || row.FloorNumber <= 0 || string.IsNullOrWhiteSpace(row.FlatNumber))
        {
            Fail(result, row, "Building, Floor Number and Flat Number are required.");
            return;
        }

        try
        {
            var building = await _context.Buildings.FirstOrDefaultAsync(
                b => b.SocietyId == societyId && !b.IsDeleted && b.Name.ToLower() == row.Building.Trim().ToLower(), ct);
            if (building is null)
            {
                building = new Building { SocietyId = societyId, Name = row.Building.Trim() };
                await _context.Buildings.AddAsync(building, ct);
                await _context.SaveChangesAsync(ct);
            }

            var wingName = string.IsNullOrWhiteSpace(row.Wing) ? "Main" : row.Wing.Trim();
            var wing = await _context.Wings.FirstOrDefaultAsync(
                w => w.BuildingId == building.Id && !w.IsDeleted && w.Name.ToLower() == wingName.ToLower(), ct);
            if (wing is null)
            {
                wing = new Wing { BuildingId = building.Id, Name = wingName };
                await _context.Wings.AddAsync(wing, ct);
                await _context.SaveChangesAsync(ct);
            }

            var floor = await _context.Floors.FirstOrDefaultAsync(
                f => f.WingId == wing.Id && !f.IsDeleted && f.FloorNumber == row.FloorNumber, ct);
            if (floor is null)
            {
                floor = new Floor { WingId = wing.Id, FloorNumber = row.FloorNumber };
                await _context.Floors.AddAsync(floor, ct);
                await _context.SaveChangesAsync(ct);
            }

            var flatNumber = row.FlatNumber.Trim();
            var flatType = ParseFlatType(row.FlatTypeLabel);
            var flat = await _context.Flats.FirstOrDefaultAsync(
                f => f.FloorId == floor.Id && !f.IsDeleted && f.FlatNumber == flatNumber, ct);
            var flatCreated = false;
            if (flat is null)
            {
                flat = new Flat
                {
                    FloorId = floor.Id, FlatNumber = flatNumber, FlatType = flatType ?? FlatType.TwoBHK,
                    AreaSqFt = row.AreaSqFt, Status = row.IsVacant ? FlatStatus.Vacant : FlatStatus.Occupied
                };
                await _context.Flats.AddAsync(flat, ct);
                await _context.SaveChangesAsync(ct);
                flatCreated = true;
                result.FlatsCreated++;
            }
            else
            {
                if (flatType.HasValue) flat.FlatType = flatType.Value;
                if (row.AreaSqFt.HasValue) flat.AreaSqFt = row.AreaSqFt;
                if (!row.IsVacant) flat.Status = FlatStatus.Occupied;
                await _context.SaveChangesAsync(ct);
            }

            if (row.IsVacant || string.IsNullOrWhiteSpace(row.OwnerFirstName) || string.IsNullOrWhiteSpace(row.OwnerPhone))
            {
                result.RowResults.Add(new ResidentImportRowResultDto
                {
                    RowNumber = row.RowNumber, Status = flatCreated ? "Created" : "Updated",
                    Message = $"Flat {flatNumber} {(flatCreated ? "created" : "updated")} (vacant — no owner recorded)."
                });
                return;
            }

            var phone = row.OwnerPhone.Trim();
            var member = await _context.Members.FirstOrDefaultAsync(
                m => m.SocietyId == societyId && !m.IsDeleted && m.Phone == phone, ct);
            var memberCreated = false;
            if (member is null)
            {
                member = new Member
                {
                    SocietyId = societyId, FirstName = row.OwnerFirstName.Trim(),
                    LastName = row.OwnerLastName?.Trim() ?? "", Phone = phone,
                    Email = string.IsNullOrWhiteSpace(row.OwnerEmail) ? null : row.OwnerEmail.Trim()
                };
                await _context.Members.AddAsync(member, ct);
                await _context.SaveChangesAsync(ct);
                memberCreated = true;
                result.MembersCreated++;
            }

            var hasCurrentResidency = await _context.FlatResidencies.AnyAsync(
                r => r.FlatId == flat.Id && r.MemberId == member.Id && !r.IsDeleted && r.MoveOutDate == null, ct);
            var residencyCreated = false;
            if (!hasCurrentResidency)
            {
                var hasCurrentPrimary = await _context.FlatResidencies.AnyAsync(
                    r => r.FlatId == flat.Id && !r.IsDeleted && r.MoveOutDate == null && r.IsPrimaryContact, ct);

                await _context.FlatResidencies.AddAsync(new FlatResidency
                {
                    FlatId = flat.Id, MemberId = member.Id,
                    MemberType = ParseMemberType(row.MemberTypeLabel) ?? MemberType.Owner,
                    MoveInDate = row.MoveInDate ?? DateTime.UtcNow.Date, IsPrimaryContact = !hasCurrentPrimary
                }, ct);
                await _context.SaveChangesAsync(ct);
                residencyCreated = true;
                result.ResidenciesCreated++;
            }

            var parts = new List<string> { $"Flat {flatNumber} {(flatCreated ? "created" : "matched")}" };
            parts.Add(memberCreated ? $"owner {member.FullName} created" : $"owner {member.FullName} matched by phone");
            parts.Add(residencyCreated ? "residency linked" : "already linked");
            result.RowResults.Add(new ResidentImportRowResultDto
            {
                RowNumber = row.RowNumber, Status = flatCreated || memberCreated || residencyCreated ? "Created" : "Skipped",
                Message = string.Join("; ", parts) + "."
            });
        }
        catch (Exception ex)
        {
            Fail(result, row, $"Unexpected error: {ex.Message}");
        }
    }

    private static void Fail(ResidentImportResultDto result, ResidentImportRowDto row, string message)
    {
        result.RowsFailed++;
        result.RowResults.Add(new ResidentImportRowResultDto { RowNumber = row.RowNumber, Status = "Error", Message = message });
    }

    private static FlatType? ParseFlatType(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var normalized = label.Replace(" ", "").Replace("-", "");
        return FlatTypeLookup.TryGetValue(normalized, out var type) ? type : null;
    }

    private static MemberType? ParseMemberType(string? label) =>
        !string.IsNullOrWhiteSpace(label) && MemberTypeLookup.TryGetValue(label.Trim(), out var type) ? type : null;
}

// ---- Query ---------------------------------------------------------------------
public record GetResidentImportTemplateQuery : IRequest<byte[]>;

public class GetResidentImportTemplateQueryHandler : IRequestHandler<GetResidentImportTemplateQuery, byte[]>
{
    private readonly IResidentImportService _importService;

    public GetResidentImportTemplateQueryHandler(IResidentImportService importService)
    {
        _importService = importService;
    }

    public Task<byte[]> Handle(GetResidentImportTemplateQuery request, CancellationToken ct) =>
        Task.FromResult(_importService.GenerateTemplate());
}
