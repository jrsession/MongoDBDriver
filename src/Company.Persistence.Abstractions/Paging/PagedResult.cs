// =============================================================================
// FILE: Paging/PagedResult.cs
// PURPOSE: Pagination response with items and metadata
// =============================================================================

namespace Company.Persistence.Abstractions.Paging;

/// <summary>
/// Represents a paginated collection of items with metadata.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
/// <param name="Items">The items for the current page.</param>
/// <param name="Page">The current page number (1-based).</param>
/// <param name="PageSize">The requested page size.</param>
/// <param name="TotalCount">Total number of items across all pages.</param>
/// <remarks>
/// <para><b>Design Decision:</b> Record with computed properties for common pagination metadata.</para>
/// <para><b>Items Collection:</b> Uses <see cref="IReadOnlyList{T}"/> for indexed access
/// without allowing mutation. The list is materialized (not lazy).</para>
/// </remarks>
/// <example>
/// <code>
/// var result = await repository.GetPagedAsync(request, ct);
///
/// result.Match(
///     success: paged =>
///     {
///         Console.WriteLine($"Page {paged.Page} of {paged.TotalPages}");
///         Console.WriteLine($"Showing {paged.Items.Count} of {paged.TotalCount} items");
///
///         foreach (var item in paged.Items)
///         {
///             Console.WriteLine(item);
///         }
///
///         if (paged.HasNextPage)
///         {
///             var nextPageRequest = request.NextPage();
///             // Fetch next page...
///         }
///     },
///     failure: error => Console.WriteLine($"Error: {error.Message}")
/// );
///
/// // Map to DTOs
/// PagedResult&lt;CustomerDto&gt; dtos = paged.Map(c => new CustomerDto(c));
/// </code>
/// </example>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    /// <remarks>
    /// Calculated as ceiling of <see cref="TotalCount"/> / <see cref="PageSize"/>.
    /// Returns 0 if <see cref="PageSize"/> is 0 (edge case).
    /// </remarks>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling((double)TotalCount / PageSize)
        : 0;

    /// <summary>
    /// Gets whether there is a next page available.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets whether there is a previous page available.
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets whether this page contains any items.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Gets whether the result set is empty (no items on any page).
    /// </summary>
    public bool IsEmpty => TotalCount == 0;

    /// <summary>
    /// Gets the 1-based index of the first item on this page.
    /// </summary>
    /// <remarks>
    /// Returns 0 if the page is empty.
    /// </remarks>
    public int FirstItemIndex => HasItems ? (Page - 1) * PageSize + 1 : 0;

    /// <summary>
    /// Gets the 1-based index of the last item on this page.
    /// </summary>
    /// <remarks>
    /// Returns 0 if the page is empty.
    /// </remarks>
    public int LastItemIndex => HasItems ? FirstItemIndex + Items.Count - 1 : 0;

    /// <summary>
    /// Creates an empty paged result for the specified request.
    /// </summary>
    /// <param name="request">The pagination request.</param>
    /// <returns>An empty <see cref="PagedResult{T}"/> with zero items and total count.</returns>
    /// <example>
    /// <code>
    /// if (noResultsFound)
    /// {
    ///     return PagedResult&lt;Customer&gt;.Empty(request);
    /// }
    /// </code>
    /// </example>
    public static PagedResult<T> Empty(PagedRequest request) =>
        new([], request.Page, request.PageSize, 0);

    /// <summary>
    /// Transforms the items using the specified mapper function.
    /// </summary>
    /// <typeparam name="TNew">The type of the transformed items.</typeparam>
    /// <param name="mapper">Function to transform each item.</param>
    /// <returns>A new <see cref="PagedResult{T}"/> with transformed items.</returns>
    /// <remarks>
    /// Pagination metadata (Page, PageSize, TotalCount) is preserved.
    /// </remarks>
    /// <example>
    /// <code>
    /// PagedResult&lt;CustomerDto&gt; dtos = customers.Map(c => new CustomerDto
    /// {
    ///     Id = c.Id,
    ///     Name = c.Name,
    ///     Email = c.Email
    /// });
    /// </code>
    /// </example>
    public PagedResult<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var mappedItems = Items.Select(mapper).ToList();
        return new PagedResult<TNew>(mappedItems, Page, PageSize, TotalCount);
    }

    /// <summary>
    /// Transforms the items using the specified async mapper function.
    /// </summary>
    /// <typeparam name="TNew">The type of the transformed items.</typeparam>
    /// <param name="mapper">Async function to transform each item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new <see cref="PagedResult{T}"/> with transformed items.</returns>
    public async Task<PagedResult<TNew>> MapAsync<TNew>(
        Func<T, CancellationToken, Task<TNew>> mapper,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        var mappedItems = new List<TNew>(Items.Count);
        foreach (var item in Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mappedItems.Add(await mapper(item, cancellationToken).ConfigureAwait(false));
        }

        return new PagedResult<TNew>(mappedItems, Page, PageSize, TotalCount);
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"Page {Page}/{TotalPages} ({Items.Count} items, {TotalCount} total)";
}
