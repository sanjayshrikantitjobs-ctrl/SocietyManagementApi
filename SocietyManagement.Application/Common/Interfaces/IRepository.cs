using System.Linq.Expressions;
using SocietyManagement.Domain.Common;

namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>
/// Generic repository over any BaseAuditableEntity. Kept intentionally small
/// (Clean Architecture "Repository Pattern" requirement) — anything more
/// specific than these primitives belongs in an IQueryable projection inside a
/// query handler, not as a bespoke repository method, to avoid the classic
/// "repository interface grows one method per screen" problem.
/// </summary>
public interface IRepository<T> where T : BaseAuditableEntity
{
    IQueryable<T> Query(bool includeDeleted = false);
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity); // performs a soft delete via IsDeleted flag, see UnitOfWork
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
