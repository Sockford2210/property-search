namespace PropertySearch.Domain;

/// <summary>
/// Base type for persisted entities. Carries the surrogate key and generic
/// audit timestamps. <see cref="CreatedAt"/> is set by the database default on
/// insert; <see cref="UpdatedAt"/> is maintained by the DbContext on save.
/// </summary>
public abstract class AuditableEntity
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
