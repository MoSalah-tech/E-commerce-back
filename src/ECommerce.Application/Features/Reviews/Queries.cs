using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Mapping;
using MediatR;

namespace ECommerce.Application.Features.Reviews;

public sealed record GetProductReviewsQuery(
    Guid ProductId,
    bool ApprovedOnly = true,
    PagedRequest? Paging = null) : IRequest<Result<PagedResult<ReviewDto>>>;

public sealed class GetProductReviewsQueryHandler(IReviewRepository reviews)
    : IRequestHandler<GetProductReviewsQuery, Result<PagedResult<ReviewDto>>>
{
    public async Task<Result<PagedResult<ReviewDto>>> Handle(
        GetProductReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var paging = request.Paging ?? new PagedRequest();
        var page = await reviews.GetByProductAsync(request.ProductId, request.ApprovedOnly, paging, cancellationToken);

        return Result.Success(page.Map(x => x.ToDto()));
    }
}