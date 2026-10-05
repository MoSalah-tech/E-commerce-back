using ECommerce.Api.Contracts;
using ECommerce.Api.Extensions;
using ECommerce.Application.Common;
using ECommerce.Application.Features.Reviews;
using ECommerce.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

/// <summary>Customer product reviews and staff moderation.</summary>
[Authorize]
[Route("api/reviews")]
public sealed class ReviewsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForProduct(
        [FromQuery] Guid productId,
        [FromQuery] bool approvedOnly = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetProductReviewsQuery(
            productId,
            approvedOnly,
            new PagedRequest(
                pageNumber < 1 ? 1 : pageNumber,
                pageSize < 1 ? PagedRequest.DefaultPageSize : pageSize));

        var result = await mediator.Send(query, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateReviewCommand(request.ProductId, request.Rating, request.Title, request.Comment),
            cancellationToken);

        return result.CreatedAt(this, nameof(GetForProduct), new { productId = request.ProductId });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateReviewCommand(id, request.Rating, request.Title, request.Comment),
            cancellationToken);

        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteReviewCommand(id), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/moderation")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Moderate(Guid id, [FromBody] ModerateReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ModerateReviewCommand(id, request.IsApproved), cancellationToken);
        return result.ToActionResult(this);
    }
}