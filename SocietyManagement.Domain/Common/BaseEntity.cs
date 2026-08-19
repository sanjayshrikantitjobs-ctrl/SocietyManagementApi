namespace SocietyManagement.Domain.Common;

/// <summary>
/// Base class for all domain entities. Carries an integer surrogate key and a
/// domain-event buffer so aggregate roots can raise events that Infrastructure
/// dispatches after SaveChanges succeeds (see DispatchDomainEventsInterceptor).
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    private readonly List<BaseEvent> _domainEvents = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(BaseEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void RemoveDomainEvent(BaseEvent domainEvent) => _domainEvents.Remove(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
