using ECommerce.Application.Common;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Interfaces;

/// <summary>
/// Customer lookups are keyed by the identity user id, which is the only
/// identifier a request handler has access to.
/// </summary>
public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Loads the customer together with the cart and every cart line's product.</summary>
    Task<Customer?> GetWithCartAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Customer?> GetWithAddressesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Customer?> GetWithOrdersAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetWithItemsAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Order?> GetWithItemsByNumberAsync(string orderNumber, CancellationToken cancellationToken = default);

    Task<PagedResult<Order>> GetPagedAsync(OrderFilter filter, CancellationToken cancellationToken = default);
}

public interface IReviewRepository : IRepository<Review>
{
    Task<Review?> GetByProductAndCustomerAsync(
        Guid productId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Review>> GetByProductAsync(
        Guid productId,
        bool approvedOnly,
        PagedRequest paging,
        CancellationToken cancellationToken = default);

    /// <summary>Average rating and review count over approved reviews only.</summary>
    Task<(decimal AverageRating, int Count)> GetRatingSummaryAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}