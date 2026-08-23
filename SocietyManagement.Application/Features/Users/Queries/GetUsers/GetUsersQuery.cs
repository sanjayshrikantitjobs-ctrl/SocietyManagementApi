using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Application.Features.Users.Common;
using SocietyManagement.Shared.Constants;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.Application.Features.Users.Queries.GetUsers;

/// <summary>Paginated, searchable, filterable user list backing the Angular
/// shared data-table component (search + role filter + active/locked filter).</summary>
public record GetUsersQuery(
    string? Search,
    int? RoleId,
    bool? IsActive,
    string? SortBy = null,
    bool SortDescending = false,
    int PageNumber = 1,
    int PageSize = AppConstants.DefaultPageSize) : IRequest<PaginatedResult<UserDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PaginatedResult<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetUsersQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<PaginatedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.MobileNumber.Contains(term));
        }

        if (request.RoleId.HasValue)
        {
            query = query.Where(u => u.RoleId == request.RoleId);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(request.PageSize, 1, AppConstants.MaxPageSize);
        var pageNumber = Math.Max(request.PageNumber, 1);

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("name", false) => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
            ("name", true) => query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName),
            ("role", false) => query.OrderBy(u => u.Role.Name),
            ("role", true) => query.OrderByDescending(u => u.Role.Name),
            ("status", false) => query.OrderBy(u => u.IsLocked).ThenBy(u => u.IsActive),
            ("status", true) => query.OrderByDescending(u => u.IsLocked).ThenByDescending(u => u.IsActive),
            ("lastlogin", false) => query.OrderBy(u => u.LastLoginAt),
            ("lastlogin", true) => query.OrderByDescending(u => u.LastLoginAt),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<UserDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<UserDto>(items, totalCount, pageNumber, pageSize);
    }
}
