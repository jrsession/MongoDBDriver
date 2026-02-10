// =============================================================================
// FILE: Paging/PagedRequest.cs
// PURPOSE: Pagination parameters with built-in validation and safety limits
// =============================================================================

namespace Company.Persistence.Abstractions.Paging;

/// <summary>
/// Represents pagination parameters for list queries.
/// </summary>
/// <remarks>
/// <para><b>Design Decision:</b> Enforces pagination on all list queries to prevent
/// unbounded queries that could exhaust memory or timeout.</para>
/// <para><b>Validation:</b> Invalid values are clamped to valid ranges rather than throwing,
/// following the robustness principle (be conservative in what you send, liberal in what you accept).</para>
/// <para><b>Safety:</b> <see cref="MaxPageSize"/> prevents accidentally requesting millions of records.</para>
/// </remarks>
/// <example>
/// <code>
/// // Default: page 1, 20 items
/// var request = new PagedRequest();
///
/// // Custom pagination
/// var request = new PagedRequest(Page: 2, PageSize: 50);
///
/// // PageSize is automatically clamped to MaxPageSize
/// var request = new PagedRequest(PageSize: 500);  // Becomes 100
///
/// // Page is automatically clamped to minimum 1
/// var request = new PagedRequest(Page: 0);  // Becomes 1
///
/// // Navigate pages
/// var nextPage = request.NextPage();
/// var prevPage = request.PreviousPage();
/// </code>
/// </example>
public readonly record struct PagedRequest
{
    /// <summary>
    /// Maximum allowed page size to prevent unbounded queries.
    /// </summary>
    /// <remarks>
    /// This is a hard limit. Requests for larger page sizes are silently clamped.
    /// </remarks>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Default page size when not specified.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Minimum valid page number.
    /// </summary>
    public const int MinPage = 1;

    private readonly int _page;
    private readonly int _pageSize;

    /// <summary>
    /// Creates a new paged request with the specified parameters.
    /// </summary>
    /// <param name="Page">1-based page number. Values less than 1 are clamped to 1.</param>
    /// <param name="PageSize">Items per page. Values are clamped between 1 and <see cref="MaxPageSize"/>.</param>
    public PagedRequest(int Page = MinPage, int PageSize = DefaultPageSize)
    {
        _page = Math.Max(MinPage, Page);
        _pageSize = Math.Clamp(PageSize, 1, MaxPageSize);
    }

    /// <summary>
    /// Gets the 1-based page number (always >= 1).
    /// </summary>
    public int Page => _page == 0 ? MinPage : _page;

    /// <summary>
    /// Gets the items per page (always between 1 and <see cref="MaxPageSize"/>).
    /// </summary>
    public int PageSize => _pageSize == 0 ? DefaultPageSize : _pageSize;

    /// <summary>
    /// Gets the number of items to skip for offset-based pagination.
    /// </summary>
    /// <remarks>
    /// Use this for MongoDB's <c>Skip()</c> or SQL's <c>OFFSET</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Page 1, size 20 -> Skip 0
    /// // Page 2, size 20 -> Skip 20
    /// // Page 3, size 20 -> Skip 40
    /// var skip = request.Skip;
    /// </code>
    /// </example>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Creates a request for the next page with the same page size.
    /// </summary>
    /// <returns>A new <see cref="PagedRequest"/> for the next page.</returns>
    public PagedRequest NextPage() => new(Page + 1, PageSize);

    /// <summary>
    /// Creates a request for the previous page (minimum page 1) with the same page size.
    /// </summary>
    /// <returns>A new <see cref="PagedRequest"/> for the previous page.</returns>
    public PagedRequest PreviousPage() => new(Math.Max(MinPage, Page - 1), PageSize);

    /// <summary>
    /// Creates a request for a specific page with the same page size.
    /// </summary>
    /// <param name="page">The page number to navigate to.</param>
    /// <returns>A new <see cref="PagedRequest"/> for the specified page.</returns>
    public PagedRequest WithPage(int page) => new(page, PageSize);

    /// <summary>
    /// Creates a request with a different page size (clamped to valid range).
    /// </summary>
    /// <param name="pageSize">The new page size.</param>
    /// <returns>A new <see cref="PagedRequest"/> with the specified page size.</returns>
    public PagedRequest WithPageSize(int pageSize) => new(Page, pageSize);

    /// <inheritdoc />
    public override string ToString() => $"Page {Page}, Size {PageSize}";
}
