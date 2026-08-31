using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Helpers;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Vehicles;

// ---- DTOs ----------------------------------------------------------------------

/// <summary>Result of confirming a scan or opening a search hit. Owner
/// fields are only populated when the caller holds Vehicles.ViewOwnerDetails
/// — Watchman logins get vehicle/flat/building/wing/parking/status only.</summary>
public class VehicleScanResultDto
{
    public int ScanLogId { get; set; }
    public VehicleScanResultStatus Result { get; set; }
    public string RegistrationNumber { get; set; } = default!;

    public int? VehicleId { get; set; }
    public VehicleType? VehicleType { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }

    public int? FlatId { get; set; }
    public string? FlatNumber { get; set; }
    public string? WingName { get; set; }
    public string? BuildingName { get; set; }

    public string? ParkingSlotNumber { get; set; }
    public ParkingStatus? ParkingStatus { get; set; }

    public string? OwnerName { get; set; }
    public string? OwnerPhone { get; set; }
    public string? OwnerEmail { get; set; }
}

/// <summary>Lightweight row for the manual-search results list — opening one
/// (calling ConfirmVehicleScanCommand with Source=ManualSearch) is what
/// actually logs the lookup and returns the full gated VehicleScanResultDto.</summary>
public class VehicleSearchItemDto
{
    public int VehicleId { get; set; }
    public string RegistrationNumber { get; set; } = default!;
    public VehicleType VehicleType { get; set; }
    public string? FlatNumber { get; set; }
}

public class VehicleScanHistoryDto
{
    public int Id { get; set; }
    public DateTime ScannedAt { get; set; }
    public VehicleScanSource Source { get; set; }
    public string NormalizedRegistrationNumber { get; set; } = default!;
    public double? Confidence { get; set; }
    public VehicleScanResultStatus Result { get; set; }
    public string ScannedByName { get; set; } = default!;
    public string? GateName { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Ephemeral OCR-assist result for the drag-to-crop step — never
/// persisted by itself. The caller (guard/resident) reviews/edits the
/// NormalizedText before it's ever sent on to ConfirmVehicleScanCommand.</summary>
public class PlateOcrResultDto
{
    public string RecognizedText { get; set; } = default!;
    public string NormalizedText { get; set; } = default!;
    public double Confidence { get; set; }
}

// ---- Commands --------------------------------------------------------------------

public record ConfirmVehicleScanCommand(
    int SocietyId, string NormalizedRegistrationNumber, string? RawOcrText, double? Confidence,
    VehicleScanSource Source, int? GateId, byte[]? ImageBytes) : IRequest<VehicleScanResultDto>;

public class ConfirmVehicleScanCommandValidator : AbstractValidator<ConfirmVehicleScanCommand>
{
    public ConfirmVehicleScanCommandValidator()
    {
        RuleFor(x => x.NormalizedRegistrationNumber).NotEmpty().MaximumLength(20);
    }
}

// ---- Queries -------------------------------------------------------------------

public record GetVehicleSearchQuery(int SocietyId, string Query) : IRequest<List<VehicleSearchItemDto>>;

public record GetScanHistoryQuery(
    int SocietyId, DateTime? FromDate, DateTime? ToDate, VehicleScanResultStatus? Result,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<VehicleScanHistoryDto>>;

/// <summary>Runs OCR on the full photo, perspective-correcting the plate
/// region the user marked (Corners, in order TopLeft/TopRight/BottomRight/
/// BottomLeft, in the photo's own natural pixel space). Never touches the
/// database — see PlateOcrResultDto.</summary>
public record RecognizePlateQuery(byte[] ImageBytes, IReadOnlyList<PlatePoint> Corners) : IRequest<PlateOcrResultDto>;

// ---- Handlers --------------------------------------------------------------------

public class VehicleScanHandlers :
    IRequestHandler<ConfirmVehicleScanCommand, VehicleScanResultDto>,
    IRequestHandler<GetVehicleSearchQuery, List<VehicleSearchItemDto>>,
    IRequestHandler<GetScanHistoryQuery, PaginatedResult<VehicleScanHistoryDto>>,
    IRequestHandler<RecognizePlateQuery, PlateOcrResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorage;
    private readonly IDateTime _dateTime;
    private readonly IVehiclePlateOcrService _plateOcr;

    public VehicleScanHandlers(
        IApplicationDbContext context, ICurrentUserService currentUserService,
        IFileStorageService fileStorage, IDateTime dateTime, IVehiclePlateOcrService plateOcr)
    {
        _context = context;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
        _dateTime = dateTime;
        _plateOcr = plateOcr;
    }

    public async Task<PlateOcrResultDto> Handle(RecognizePlateQuery request, CancellationToken ct)
    {
        var result = await _plateOcr.RecognizeAsync(request.ImageBytes, request.Corners, ct);
        return new PlateOcrResultDto
        {
            RecognizedText = result.RecognizedText, NormalizedText = result.NormalizedText, Confidence = result.Confidence
        };
    }

    public async Task<VehicleScanResultDto> Handle(ConfirmVehicleScanCommand request, CancellationToken ct)
    {
        var normalized = VehicleNumberNormalizer.Normalize(request.NormalizedRegistrationNumber);

        var vehicle = await _context.Vehicles
            .Where(v => !v.IsDeleted)
            .Where(v =>
                (v.Member != null && v.Member.SocietyId == request.SocietyId) ||
                (v.Flat != null && v.Flat.Floor.Wing.Building.SocietyId == request.SocietyId))
            .ToListAsync(ct); // registration numbers aren't stored pre-normalized — compare in memory, society-scoped result set is small
        var matched = vehicle.FirstOrDefault(v => VehicleNumberNormalizer.Normalize(v.RegistrationNumber) == normalized);

        string? imageUrl = null;
        if (request.ImageBytes is { Length: > 0 })
        {
            imageUrl = await _fileStorage.SaveAsync(request.ImageBytes, $"{normalized}.jpg", "vehicle-scans", ct);
        }

        var log = new VehicleScanLog
        {
            SocietyId = request.SocietyId,
            GateId = request.GateId,
            ScannedByUserId = _currentUserService.UserId!.Value,
            ScannedAt = _dateTime.UtcNow,
            Source = request.Source,
            RawOcrText = request.RawOcrText,
            NormalizedRegistrationNumber = normalized,
            Confidence = request.Confidence,
            ImageUrl = imageUrl,
            MatchedVehicleId = matched?.Id,
            Result = matched != null ? VehicleScanResultStatus.Matched : VehicleScanResultStatus.NotRegistered
        };
        await _context.VehicleScanLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);

        if (matched == null)
        {
            return new VehicleScanResultDto
            {
                ScanLogId = log.Id, Result = VehicleScanResultStatus.NotRegistered, RegistrationNumber = normalized
            };
        }

        return await BuildResultDtoAsync(log.Id, matched, normalized, ct);
    }

    public async Task<List<VehicleSearchItemDto>> Handle(GetVehicleSearchQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query)) return new List<VehicleSearchItemDto>();
        var term = request.Query.Trim().ToLower();

        return await _context.Vehicles
            .Where(v => !v.IsDeleted)
            .Where(v =>
                (v.Member != null && v.Member.SocietyId == request.SocietyId) ||
                (v.Flat != null && v.Flat.Floor.Wing.Building.SocietyId == request.SocietyId))
            .Where(v => v.RegistrationNumber.ToLower().Contains(term)
                || (v.Member != null && (v.Member.FirstName.ToLower().Contains(term) || v.Member.LastName.ToLower().Contains(term)))
                || (v.Flat != null && v.Flat.FlatNumber.ToLower().Contains(term)))
            .OrderBy(v => v.RegistrationNumber)
            .Take(20)
            .Select(v => new VehicleSearchItemDto
            {
                VehicleId = v.Id, RegistrationNumber = v.RegistrationNumber, VehicleType = v.VehicleType,
                FlatNumber = v.Flat != null ? v.Flat.FlatNumber : null
            })
            .ToListAsync(ct);
    }

    public async Task<PaginatedResult<VehicleScanHistoryDto>> Handle(GetScanHistoryQuery request, CancellationToken ct)
    {
        var query = _context.VehicleScanLogs.Where(l => l.SocietyId == request.SocietyId);

        // Watchman sees only their own scans; Admin/Super Admin see the whole society.
        if (_currentUserService.RoleName == SocietyManagement.Shared.Constants.Roles.Watchman)
        {
            query = query.Where(l => l.ScannedByUserId == _currentUserService.UserId);
        }

        if (request.FromDate.HasValue) query = query.Where(l => l.ScannedAt >= request.FromDate);
        if (request.ToDate.HasValue) query = query.Where(l => l.ScannedAt <= request.ToDate);
        if (request.Result.HasValue) query = query.Where(l => l.Result == request.Result);

        var totalCount = await query.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var items = await query
            .OrderByDescending(l => l.ScannedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new VehicleScanHistoryDto
            {
                Id = l.Id, ScannedAt = l.ScannedAt, Source = l.Source,
                NormalizedRegistrationNumber = l.NormalizedRegistrationNumber, Confidence = l.Confidence,
                Result = l.Result, ScannedByName = l.ScannedByUser.FirstName + " " + l.ScannedByUser.LastName,
                GateName = l.Gate != null ? l.Gate.Name : null, ImageUrl = l.ImageUrl
            })
            .ToListAsync(ct);

        return new PaginatedResult<VehicleScanHistoryDto>(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>Resolves display fields for a matched vehicle: flat/building/wing
    /// (via Vehicle.FlatId directly, or — for the legacy Member-linked path — via
    /// the member's current, non-moved-out FlatResidency), parking, and current
    /// owner contact info (redacted unless the caller holds ViewOwnerDetails).</summary>
    private async Task<VehicleScanResultDto> BuildResultDtoAsync(int scanLogId, Vehicle vehicle, string normalized, CancellationToken ct)
    {
        var dto = new VehicleScanResultDto
        {
            ScanLogId = scanLogId, Result = VehicleScanResultStatus.Matched, RegistrationNumber = normalized,
            VehicleId = vehicle.Id, VehicleType = vehicle.VehicleType, Make = vehicle.Make, Model = vehicle.Model,
            Color = vehicle.Color
        };

        var flatId = vehicle.FlatId;
        if (flatId == null && vehicle.MemberId.HasValue)
        {
            flatId = await _context.FlatResidencies
                .Where(r => r.MemberId == vehicle.MemberId && r.MoveOutDate == null && !r.IsDeleted)
                .Select(r => (int?)r.FlatId)
                .FirstOrDefaultAsync(ct);
        }

        if (flatId.HasValue)
        {
            var flatInfo = await _context.Flats
                .Where(f => f.Id == flatId)
                .Select(f => new
                {
                    f.FlatNumber, WingName = f.Floor.Wing.Name, BuildingName = f.Floor.Wing.Building.Name,
                    ParkingSlotNumber = f.ParkingSlots.Select(p => (string?)p.SlotNumber).FirstOrDefault(),
                    ParkingStatus = f.ParkingSlots.Select(p => (ParkingStatus?)p.Status).FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);

            if (flatInfo != null)
            {
                dto.FlatId = flatId;
                dto.FlatNumber = flatInfo.FlatNumber;
                dto.WingName = flatInfo.WingName;
                dto.BuildingName = flatInfo.BuildingName;
                dto.ParkingSlotNumber = flatInfo.ParkingSlotNumber;
                dto.ParkingStatus = flatInfo.ParkingStatus;
            }
        }

        if (vehicle.ParkingSlotId.HasValue && dto.ParkingSlotNumber == null)
        {
            var slot = await _context.ParkingSlots.Where(p => p.Id == vehicle.ParkingSlotId)
                .Select(p => new { p.SlotNumber, p.Status }).FirstOrDefaultAsync(ct);
            if (slot != null) { dto.ParkingSlotNumber = slot.SlotNumber; dto.ParkingStatus = slot.Status; }
        }

        if (!_currentUserService.HasPermission(SocietyManagement.Shared.Constants.Permissions.Vehicles.ViewOwnerDetails))
        {
            return dto;
        }

        if (vehicle.MemberId.HasValue)
        {
            var member = await _context.Members.Where(m => m.Id == vehicle.MemberId)
                .Select(m => new { Name = m.FirstName + " " + m.LastName, m.Phone, m.Email }).FirstOrDefaultAsync(ct);
            if (member != null) { dto.OwnerName = member.Name; dto.OwnerPhone = member.Phone; dto.OwnerEmail = member.Email; }
        }
        else if (dto.FlatId.HasValue)
        {
            var owner = await _context.OccupancyMembers
                .Where(m => !m.IsDeleted && m.LeftDate == null && m.FlatOccupancy.Type == OccupancyType.Owner
                    && m.FlatOccupancy.EndDate == null && m.FlatOccupancy.FlatId == dto.FlatId)
                .OrderByDescending(m => m.IsPrimary)
                .Select(m => new { Name = m.Person.FirstName + " " + m.Person.LastName, m.Person.Phone, m.Person.Email })
                .FirstOrDefaultAsync(ct);
            if (owner != null) { dto.OwnerName = owner.Name; dto.OwnerPhone = owner.Phone; dto.OwnerEmail = owner.Email; }
        }

        return dto;
    }
}
