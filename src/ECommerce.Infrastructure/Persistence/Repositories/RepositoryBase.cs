using ECommerce.Application.Interfaces;
using ECommerce.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Shared EF Core implementation for aggregate-root repositories.
/// Deletion is always a soft delete, which keeps historical orders intact.
/// </summary>
public abstract class RepositoryBase<TEntity>(AppDbContext context) : IRepository<TEntity>
    where TEntity : Entity, IAggregateRoot
{
    protected AppDbContext Context { get; } = context;

    protected DbSet<TEntity> Entities => Context.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Entities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public virtual Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Entities.AnyAsync(x => x.Id == id, cancellationToken);

    public virtual Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Entities.CountAsync(cancellationToken);

    public void Add(TEntity aggregate) => Entities.Add(aggregate);

    public void Update(TEntity aggregate) => Entities.Update(aggregate);

    public void Remove(TEntity aggregate)
    {
        aggregate.MarkAsDeleted();
        Entities.Update(aggregate);
    }
}