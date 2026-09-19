using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Pets;

public class PetDto
{
    public int Id { get; set; }
    public int SocietyId { get; set; }
    public int FlatId { get; set; }
    public string FlatNumber { get; set; } = default!;
    public string Name { get; set; } = default!;
    public PetType PetType { get; set; }
    public string? Breed { get; set; }
    public string? PhotoUrl { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Identification { get; set; }
    public DateTime? LastVaccinationDate { get; set; }
    public DateTime? NextVaccinationDue { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class PetSummaryDto
{
    public int TotalPets { get; set; }
    public int Dogs { get; set; }
    public int Cats { get; set; }
    public int VaccinationOverdue { get; set; }
    public int NeverVaccinated { get; set; }
}

public record CreatePetCommand(
    int SocietyId, int FlatId, string Name, PetType PetType, string? Breed, string? PhotoUrl, string? RegistrationNumber,
    string? Identification, DateTime? LastVaccinationDate, DateTime? NextVaccinationDue, string? Notes) : IRequest<int>;

public class CreatePetCommandValidator : AbstractValidator<CreatePetCommand>
{
    public CreatePetCommandValidator()
    {
        RuleFor(x => x.SocietyId).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Breed).MaximumLength(100);
        RuleFor(x => x.NextVaccinationDue).GreaterThanOrEqualTo(x => x.LastVaccinationDate)
            .When(x => x.LastVaccinationDate.HasValue && x.NextVaccinationDue.HasValue)
            .WithMessage("Next vaccination due can't be before the last vaccination.");
    }
}

public record UpdatePetCommand(
    int Id, int FlatId, string Name, PetType PetType, string? Breed, string? PhotoUrl, string? RegistrationNumber,
    string? Identification, DateTime? LastVaccinationDate, DateTime? NextVaccinationDue, string? Notes, bool IsActive) : IRequest<Unit>;

public class UpdatePetCommandValidator : AbstractValidator<UpdatePetCommand>
{
    public UpdatePetCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.FlatId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public record DeletePetCommand(int Id) : IRequest<Unit>;

public record GetPetsQuery(
    int SocietyId, string? Search, PetType? PetType, int? FlatId, string? SortBy = null, bool SortDescending = false,
    int PageNumber = 1, int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<PetDto>>;

public record GetPetSummaryQuery(int SocietyId) : IRequest<PetSummaryDto>;

public class PetHandlers :
    IRequestHandler<CreatePetCommand, int>,
    IRequestHandler<UpdatePetCommand, Unit>,
    IRequestHandler<DeletePetCommand, Unit>,
    IRequestHandler<GetPetsQuery, PaginatedResult<PetDto>>,
    IRequestHandler<GetPetSummaryQuery, PetSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    public PetHandlers(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUser)
    {
        _context = context;
        _auditService = auditService;
        _currentUser = currentUser;
    }

    private async Task EnsureFlatInSocietyAsync(int flatId, int societyId, CancellationToken ct)
    {
        var ok = await _context.Flats.AnyAsync(f => f.Id == flatId && !f.IsDeleted && f.Floor.Wing.Building.SocietyId == societyId, ct);
        if (!ok) throw new NotFoundException(nameof(Flat), flatId);
    }

    // Mutated by its own Id — SocietyScopeFilter can't see a SocietyId here,
    // so the caller's tenant is checked explicitly.
    private async Task<Pet> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException(nameof(Pet), id);
        if (_currentUser.SocietyId.HasValue && _currentUser.SocietyId != pet.SocietyId) throw new NotFoundException(nameof(Pet), id);
        return pet;
    }

    public async Task<int> Handle(CreatePetCommand r, CancellationToken ct)
    {
        await EnsureFlatInSocietyAsync(r.FlatId, r.SocietyId, ct);
        var pet = new Pet
        {
            SocietyId = r.SocietyId, FlatId = r.FlatId, Name = r.Name.Trim(), PetType = r.PetType, Breed = r.Breed, PhotoUrl = r.PhotoUrl,
            RegistrationNumber = r.RegistrationNumber, Identification = r.Identification, LastVaccinationDate = r.LastVaccinationDate,
            NextVaccinationDue = r.NextVaccinationDue, Notes = r.Notes, IsActive = true
        };
        await _context.Pets.AddAsync(pet, ct);
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Create, "Pets", nameof(Pet), pet.Id.ToString(), newValues: new { pet.Name, pet.PetType }, ct: ct);
        return pet.Id;
    }

    public async Task<Unit> Handle(UpdatePetCommand r, CancellationToken ct)
    {
        var pet = await LoadOwnedAsync(r.Id, ct);
        if (r.FlatId != pet.FlatId) await EnsureFlatInSocietyAsync(r.FlatId, pet.SocietyId, ct);
        pet.FlatId = r.FlatId; pet.Name = r.Name.Trim(); pet.PetType = r.PetType; pet.Breed = r.Breed; pet.PhotoUrl = r.PhotoUrl;
        pet.RegistrationNumber = r.RegistrationNumber; pet.Identification = r.Identification;
        pet.LastVaccinationDate = r.LastVaccinationDate; pet.NextVaccinationDue = r.NextVaccinationDue; pet.Notes = r.Notes; pet.IsActive = r.IsActive;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Update, "Pets", nameof(Pet), pet.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<Unit> Handle(DeletePetCommand r, CancellationToken ct)
    {
        var pet = await LoadOwnedAsync(r.Id, ct);
        pet.IsDeleted = true;
        pet.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditService.LogAsync(AuditAction.Delete, "Pets", nameof(Pet), pet.Id.ToString(), ct: ct);
        return Unit.Value;
    }

    public async Task<PaginatedResult<PetDto>> Handle(GetPetsQuery r, CancellationToken ct)
    {
        var query = _context.Pets.Where(p => p.SocietyId == r.SocietyId);
        if (r.PetType.HasValue) query = query.Where(p => p.PetType == r.PetType);
        if (r.FlatId.HasValue) query = query.Where(p => p.FlatId == r.FlatId);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || (p.Breed != null && p.Breed.ToLower().Contains(term)) ||
                                     p.Flat.FlatNumber.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        query = (r.SortBy?.ToLowerInvariant(), r.SortDescending) switch
        {
            ("name", false) => query.OrderBy(p => p.Name),
            ("name", true) => query.OrderByDescending(p => p.Name),
            ("type", false) => query.OrderBy(p => p.PetType),
            ("type", true) => query.OrderByDescending(p => p.PetType),
            ("vaccination", false) => query.OrderBy(p => p.NextVaccinationDue),
            ("vaccination", true) => query.OrderByDescending(p => p.NextVaccinationDue),
            (_, true) => query.OrderByDescending(p => p.Flat.FlatNumber),
            _ => query.OrderBy(p => p.Flat.FlatNumber).ThenBy(p => p.Name)
        };

        var pageSize = Math.Clamp(r.PageSize, 1, AppConstants.MaxPageSize);
        var page = Math.Max(r.PageNumber, 1);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(p => new PetDto
        {
            Id = p.Id, SocietyId = p.SocietyId, FlatId = p.FlatId, FlatNumber = p.Flat.FlatNumber, Name = p.Name, PetType = p.PetType,
            Breed = p.Breed, PhotoUrl = p.PhotoUrl, RegistrationNumber = p.RegistrationNumber, Identification = p.Identification,
            LastVaccinationDate = p.LastVaccinationDate, NextVaccinationDue = p.NextVaccinationDue, Notes = p.Notes, IsActive = p.IsActive
        }).ToListAsync(ct);
        return new PaginatedResult<PetDto>(items, total, page, pageSize);
    }

    public async Task<PetSummaryDto> Handle(GetPetSummaryQuery r, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var active = _context.Pets.Where(p => p.SocietyId == r.SocietyId && p.IsActive);
        return new PetSummaryDto
        {
            TotalPets = await active.CountAsync(ct),
            Dogs = await active.CountAsync(p => p.PetType == PetType.Dog, ct),
            Cats = await active.CountAsync(p => p.PetType == PetType.Cat, ct),
            VaccinationOverdue = await active.CountAsync(p => p.NextVaccinationDue != null && p.NextVaccinationDue < today, ct),
            NeverVaccinated = await active.CountAsync(p => p.LastVaccinationDate == null, ct)
        };
    }
}
