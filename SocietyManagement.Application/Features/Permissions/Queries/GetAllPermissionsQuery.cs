using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Roles.Common;

namespace SocietyManagement.Application.Features.Permissions.Queries;

/// <summary>Feeds the Role Management screen's permission matrix (Module x Action
/// checkboxes) — grouped by module in the handler so Angular can render section headers.</summary>
public record GetAllPermissionsQuery : IRequest<Dictionary<string, List<PermissionDto>>>;

public class GetAllPermissionsQueryHandler
    : IRequestHandler<GetAllPermissionsQuery, Dictionary<string, List<PermissionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetAllPermissionsQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<Dictionary<string, List<PermissionDto>>> Handle(
        GetAllPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await _context.Permissions
            .OrderBy(p => p.Module).ThenBy(p => p.Action)
            .ProjectTo<PermissionDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return permissions.GroupBy(p => p.Module).ToDictionary(g => g.Key, g => g.ToList());
    }
}
