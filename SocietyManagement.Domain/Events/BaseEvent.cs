using MediatR;

namespace SocietyManagement.Domain.Common;

/// <summary>
/// Marker base for domain events. Implements MediatR's INotification so the same
/// pipeline that handles CQRS commands/queries can also publish domain events
/// (dispatched from Infrastructure after a successful SaveChangesAsync).
/// </summary>
public abstract class BaseEvent : INotification
{
    public DateTime OccurredOn { get; protected set; } = DateTime.UtcNow;
}
