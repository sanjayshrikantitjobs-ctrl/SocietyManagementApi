using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Domain.Enums;

namespace SocietyManagement.Tests.Fakes;

public class FakeAuditService : IAuditService
{
    public Task LogAsync(
        AuditAction action, string module, string? entityName = null, string? entityId = null,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default) => Task.CompletedTask;
}
