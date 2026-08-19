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

    public GetSocietiesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<SocietyDto>> Handle(GetSocietiesQuery request, CancellationToken ct) =>
        await _context.Societies.Where(s => !s.IsDeleted)
            .Select(s => new SocietyDto
            {
                Id = s.Id,
                Name = s.Name,
                RegistrationNumber = s.RegistrationNumber,
                Address = s.Address,
                City = s.City,
                State = s.State,
                Pincode = s.Pincode,
                ContactEmail = s.ContactEmail,
                ContactPhone = s.ContactPhone,
                LogoUrl = s.LogoUrl,
                EstablishedDate = s.EstablishedDate,
                BuildingCount = s.Buildings.Count(b => !b.IsDeleted)
            })
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
}

public record GetSocietyByIdQuery(int Id) : IRequest<SocietyDto>;

public class GetSocietyByIdQueryHandler : IRequestHandler<GetSocietyByIdQuery, SocietyDto>
{
    private readonly IApplicationDbContext _context;

    public GetSocietyByIdQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<SocietyDto> Handle(GetSocietyByIdQuery request, CancellationToken ct) =>
        await _context.Societies.Where(s => s.Id == request.Id && !s.IsDeleted)
            .Select(s => new SocietyDto
            {
                Id = s.Id,
                Name = s.Name,
                RegistrationNumber = s.RegistrationNumber,
                Address = s.Address,
                City = s.City,
                State = s.State,
                Pincode = s.Pincode,
                ContactEmail = s.ContactEmail,
                ContactPhone = s.ContactPhone,
                LogoUrl = s.LogoUrl,
                EstablishedDate = s.EstablishedDate,
                BuildingCount = s.Buildings.Count(b => !b.IsDeleted)
            })
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Domain.Entities.Society), request.Id);
}
