using ECommerce.Application.Common;
using ECommerce.Domain.Common;

namespace ECommerce.Application.Interfaces;

/// <summary>
/// Non-generic persistence abstraction for an aggregate root.
/// Only roots are exposed so the persistence boundary stays explicit.
/// </summary>
public interface IRepository<TAggregate> where TAggregate : Entity, IAggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    void Add(TAggregate aggregate);

    void Update(TAggregate aggregate);

    void Remove(TAggregate aggregate);
}

/// <summary>Commits all tracked changes as a single atomic unit of work.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}