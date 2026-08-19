using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Roles.Common;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Features.Roles.Queries;

public record GetRoleByIdQuery(int Id) : IRequest<RoleDetailDto>;

public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetRoleByIdQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<RoleDetailDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken) =>
        await _context.Roles.Where(r => r.Id == request.Id && !r.IsDeleted)
            .ProjectTo<RoleDetailDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Domain.Entities.Role), request.Id);
}
