using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Societies.Common;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Societies.Queries;

public record GetSocietiesQuery : IRequest<List<SocietyDto>>;

public class GetSocietiesQueryHandler : IRequestHandler<GetSocietiesQuery, List<SocietyDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSocietiesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    /// <summary>Every existing page in this app resolves "the" society by
    /// taking GetSocieties()[0] — scoping this list to just the caller's
    /// own society (when they have one) is what makes every one of those
    /// pages correctly tenant-scoped with zero frontend changes. Super
    /// Admin (no SocietyId claim) still sees every society.</summary>
    public async Task<List<SocietyDto>> Handle(GetSocietiesQuery request, CancellationToken ct) =>
        await _context.Societies.Where(s => !s.IsDeleted
                && (_currentUserService.SocietyId == null || s.Id == _currentUserService.SocietyId))
            .Select(s => new SocietyDto
            {
                Id = s.Id,
                Name = s.Name,
                Code = s.Code,
                RegistrationNumber = s.RegistrationNumber,
                Address = s.Address,
                City = s.City,
                State = s.State,
                Pincode = s.Pincode,
                ContactEmail = s.ContactEmail,
                ContactPhone = s.ContactPhone,
                LogoUrl = s.LogoUrl,
                EstablishedDate = s.EstablishedDate,
                SubscriptionStartDate = s.SubscriptionStartDate,
                SubscriptionEndDate = s.SubscriptionEndDate,
                IsSubscriptionSuspended = s.IsSubscriptionSuspended,
                BuildingCount = s.Buildings.Count(b => !b.IsDeleted)
            })
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
}

public record GetSocietyByIdQuery(int Id) : IRequest<SocietyDto>;

public class GetSocietyByIdQueryHandler : IRequestHandler<GetSocietyByIdQuery, SocietyDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSocietyByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SocietyDto> Handle(GetSocietyByIdQuery request, CancellationToken ct)
    {
        if (_currentUserService.SocietyId.HasValue && _currentUserService.SocietyId != request.Id)
        {
            throw new ForbiddenAccessException("You can only view your own society.");
        }

        return await _context.Societies.Where(s => s.Id == request.Id && !s.IsDeleted)
            .Select(s => new SocietyDto
            {
                Id = s.Id,
                Name = s.Name,
                Code = s.Code,
                RegistrationNumber = s.RegistrationNumber,
                Address = s.Address,
                City = s.City,
                State = s.State,
                Pincode = s.Pincode,
                ContactEmail = s.ContactEmail,
                ContactPhone = s.ContactPhone,
                LogoUrl = s.LogoUrl,
                EstablishedDate = s.EstablishedDate,
                SubscriptionStartDate = s.SubscriptionStartDate,
                SubscriptionEndDate = s.SubscriptionEndDate,
                IsSubscriptionSuspended = s.IsSubscriptionSuspended,
                BuildingCount = s.Buildings.Count(b => !b.IsDeleted)
            })
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Domain.Entities.Society), request.Id);
    }
}
