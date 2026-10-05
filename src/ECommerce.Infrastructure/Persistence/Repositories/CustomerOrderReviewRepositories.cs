using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(AppDbContext context)
    : RepositoryBase<Customer>(context), ICustomerRepository
{
    public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Entities.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<Customer?> GetWithCartAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Entities
            .Include(x => x.Cart)
            .ThenInclude(x => x.Items)
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<Customer?> GetWithAddressesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Entities
            .Include(x => x.Addresses)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<Customer?> GetWithOrdersAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Entities
            .Include(x => x.Orders)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        Entities.AnyAsync(x => x.Email == email, cancellationToken);
}

public sealed class OrderRepository(AppDbContext context)
    : RepositoryBase<Order>(context), IOrderRepository
{
    public override async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await WithDetails().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Order?> GetWithItemsAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await WithDetails().FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

    public Task<Order?> GetWithItemsByNumberAsync(string orderNumber, CancellationToken cancellationToken = default) =>
        WithDetails().FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);

    public async Task<PagedResult<Order>> GetPagedAsync(
        OrderFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails();

        if (filter.UserId.HasValue)
            query = query.Where(x => x.Customer!.UserId == filter.UserId.Value);

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.OrderDate)
            .Skip(filter.Paging.Skip)
            .Take(filter.Paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, totalCount, filter.Paging.PageNumber, filter.Paging.PageSize);
    }

    private IQueryable<Order> WithDetails() =>
        Entities
            .Include(x => x.Items)
            .Include(x => x.Customer);
}

public sealed class ReviewRepository(AppDbContext context)
    : RepositoryBase<Review>(context), IReviewRepository
{
    public override async Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await WithDetails().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Review?> GetByProductAndCustomerAsync(
        Guid productId,
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        Entities.FirstOrDefaultAsync(
            x => x.ProductId == productId && x.CustomerId == customerId,
            cancellationToken);

    public async Task<PagedResult<Review>> GetByProductAsync(
        Guid productId,
        bool approvedOnly,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
    {
        var query = WithDetails().Where(x => x.ProductId == productId);

        if (approvedOnly)
            query = query.Where(x => x.IsApproved);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Review>(items, totalCount, paging.PageNumber, paging.PageSize);
    }

    public async Task<(decimal AverageRating, int Count)> GetRatingSummaryAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var query = Entities
            .Where(x => x.ProductId == productId && x.IsApproved)
            .Select(x => (int?)x.Rating);

        var ratings = await query.ToListAsync(cancellationToken);

        if (ratings.Count == 0)
            return (0m, 0);

        var average = (decimal)ratings.Sum(x => x!.Value) / ratings.Count;
        return (Math.Round(average, 2, MidpointRounding.ToEven), ratings.Count);
    }

    private IQueryable<Review> WithDetails() =>
        Entities
            .Include(x => x.Product)
            .Include(x => x.Customer);
}