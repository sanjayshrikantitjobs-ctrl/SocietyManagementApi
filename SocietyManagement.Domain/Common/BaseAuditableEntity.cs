namespace SocietyManagement.Domain.Common;

/// <summary>
/// Every persisted entity in the system inherits this so CreatedAt/CreatedBy,
/// ModifiedAt/ModifiedBy and soft-delete are handled uniformly (see
/// AuditableEntitySaveChangesInterceptor + the global HasQueryFilter for IsDeleted
/// configured in ApplicationDbContext).
/// </summary>
public abstract class BaseAuditableEntity : BaseEntity, IAuditableEntity, ISoftDelete
{
    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
    string? ModifiedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
