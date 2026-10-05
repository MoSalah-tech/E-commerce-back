namespace ECommerce.Domain.Common;

/// <summary>
/// Base type for every persisted aggregate in the domain.
/// Holds identity and audit metadata only; no infrastructure concerns leak in.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>Soft-delete flag. Enforced by an EF Core global query filter.</summary>
    public bool IsDeleted { get; protected set; }

    public void Touch() => UpdatedAt = DateTime.UtcNow;

    public void MarkAsDeleted() => IsDeleted = true;
}

/// <summary>
/// Marker for aggregate roots. Repositories are only ever defined against roots,
/// which keeps the persistence boundary explicit.
/// </summary>
public interface IAggregateRoot;