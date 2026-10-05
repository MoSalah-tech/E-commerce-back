using ECommerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence;

/// <summary>
/// Commits every tracked change atomically, so a handler that touches several
/// aggregates either fully succeeds or fully rolls back.
/// </summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken);
}