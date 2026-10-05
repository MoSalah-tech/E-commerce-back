namespace ECommerce.Application.Common;

/// <summary>Normalised paging input. Guards against negative pages and oversized page sizes.</summary>
public sealed record PagedRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public PagedRequest(int pageNumber = 1, int pageSize = DefaultPageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number must be at least 1.");

        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be at least 1.");

        PageNumber = pageNumber;
        PageSize = Math.Min(pageSize, MaxPageSize);
    }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int Skip => (PageNumber - 1) * PageSize;
}

/// <summary>A page of results plus the metadata a client needs to paginate.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public static PagedResult<T> Empty(PagedRequest request) =>
        new([], 0, request.PageNumber, request.PageSize);

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) =>
        new(Items.Select(selector).ToList(), TotalCount, PageNumber, PageSize);
}