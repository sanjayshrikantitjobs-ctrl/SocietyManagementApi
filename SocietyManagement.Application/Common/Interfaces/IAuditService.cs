using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        AuditAction action,
        string module,
        string? entityName = null,
        string? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken ct = default);
}
