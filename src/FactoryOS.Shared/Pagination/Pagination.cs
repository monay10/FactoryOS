namespace FactoryOS.Shared.Pagination;

/// <summary>The direction a result set is sorted in.</summary>
public enum SortDirection
{
    /// <summary>Ascending order (A→Z, 0→9, earliest→latest).</summary>
    Ascending = 0,

    /// <summary>Descending order (Z→A, 9→0, latest→earliest).</summary>
    Descending = 1,
}

/// <summary>
/// A request for one page of a result set: which page, how large, and how to sort. Page numbers are one-based.
/// The page size is clamped to a sane maximum so a caller cannot request an unbounded page.
/// </summary>
public sealed record PageRequest
{
    /// <summary>The largest page size a caller may request.</summary>
    public const int MaxPageSize = 200;

    /// <summary>The default page size when none is specified.</summary>
    public const int DefaultPageSize = 25;

    private PageRequest(int pageNumber, int pageSize, string? sortBy, SortDirection sortDirection)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        SortBy = sortBy;
        SortDirection = sortDirection;
    }

    /// <summary>Gets the one-based page number.</summary>
    public int PageNumber { get; }

    /// <summary>Gets the page size.</summary>
    public int PageSize { get; }

    /// <summary>Gets the optional field name to sort by.</summary>
    public string? SortBy { get; }

    /// <summary>Gets the sort direction.</summary>
    public SortDirection SortDirection { get; }

    /// <summary>Gets the number of items to skip to reach this page.</summary>
    public int Skip => (PageNumber - 1) * PageSize;

    /// <summary>Gets the number of items to take for this page.</summary>
    public int Take => PageSize;

    /// <summary>Creates a page request, normalizing the page number and clamping the page size.</summary>
    /// <param name="pageNumber">The requested page number (values below 1 become 1).</param>
    /// <param name="pageSize">The requested page size (clamped to <c>[1, <see cref="MaxPageSize"/>]</c>).</param>
    /// <param name="sortBy">The optional field to sort by.</param>
    /// <param name="sortDirection">The sort direction.</param>
    /// <returns>A normalized <see cref="PageRequest"/>.</returns>
    public static PageRequest Of(
        int pageNumber = 1,
        int pageSize = DefaultPageSize,
        string? sortBy = null,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        var normalizedPage = pageNumber < 1 ? 1 : pageNumber;
        var normalizedSize = Math.Clamp(pageSize, 1, MaxPageSize);
        return new PageRequest(normalizedPage, normalizedSize, sortBy, sortDirection);
    }
}

/// <summary>Describes where a page sits within the whole result set.</summary>
public sealed record PaginationMetadata
{
    private PaginationMetadata(long totalCount, int pageNumber, int pageSize, int totalPages)
    {
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = totalPages;
    }

    /// <summary>Gets the total number of items across all pages.</summary>
    public long TotalCount { get; }

    /// <summary>Gets the one-based page number this metadata describes.</summary>
    public int PageNumber { get; }

    /// <summary>Gets the page size.</summary>
    public int PageSize { get; }

    /// <summary>Gets the total number of pages.</summary>
    public int TotalPages { get; }

    /// <summary>Gets a value indicating whether a previous page exists.</summary>
    public bool HasPrevious => PageNumber > 1;

    /// <summary>Gets a value indicating whether a next page exists.</summary>
    public bool HasNext => PageNumber < TotalPages;

    /// <summary>Creates pagination metadata from a page request and a total count.</summary>
    /// <param name="request">The page request the page was produced for.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <returns>The computed <see cref="PaginationMetadata"/>.</returns>
    public static PaginationMetadata From(PageRequest request, long totalCount)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        return new PaginationMetadata(totalCount, request.PageNumber, request.PageSize, totalPages);
    }
}

/// <summary>One page of results together with its <see cref="PaginationMetadata"/>.</summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed record PagedResult<T>
{
    internal PagedResult(IReadOnlyList<T> items, PaginationMetadata metadata)
    {
        Items = items;
        Metadata = metadata;
    }

    /// <summary>Gets the items on this page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Gets the page's position within the whole result set.</summary>
    public PaginationMetadata Metadata { get; }
}

/// <summary>Factory for <see cref="PagedResult{T}"/> (kept off the generic type to satisfy analyzer design rules).</summary>
public static class PagedResult
{
    /// <summary>Creates a paged result.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items on this page.</param>
    /// <param name="request">The page request the items were produced for.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    /// <returns>A new <see cref="PagedResult{T}"/>.</returns>
    public static PagedResult<T> Create<T>(IReadOnlyList<T> items, PageRequest request, long totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new PagedResult<T>(items, PaginationMetadata.From(request, totalCount));
    }

    /// <summary>Creates an empty paged result for a request.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="request">The page request.</param>
    /// <returns>An empty <see cref="PagedResult{T}"/>.</returns>
    public static PagedResult<T> Empty<T>(PageRequest request) => Create<T>([], request, 0);
}
