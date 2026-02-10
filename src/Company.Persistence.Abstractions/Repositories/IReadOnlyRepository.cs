// =============================================================================
// FILE: Repositories/IReadOnlyRepository.cs
// PURPOSE: Query-only repository interface for read operations
// =============================================================================

using Company.Persistence.Abstractions.Entities;
using Company.Persistence.Abstractions.Paging;
using Company.Persistence.Abstractions.Results;

namespace Company.Persistence.Abstractions.Repositories;

/// <summary>
/// Read-only repository interface for query operations.
/// </summary>
/// <typeparam name="TEntity">The domain entity type.</typeparam>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <remarks>
/// <para><b>Design Decision:</b> Separate read interface enables:</para>
/// <list type="bullet">
///   <item><b>CQRS:</b> Query handlers only need read capabilities</item>
///   <item><b>Read replicas:</b> Can be backed by secondary database nodes</item>
///   <item><b>Testing:</b> Easier to mock when you only need read operations</item>
///   <item><b>Security:</b> Some services may only have read access</item>
/// </list>
/// <para><b>CancellationToken:</b> Required (not optional) on all methods to ensure
/// callers always propagate cancellation properly.</para>
/// <para><b>Soft Delete:</b> All query methods automatically exclude soft-deleted entities
/// when the entity implements <see cref="ISoftDeletable"/>.</para>
/// </remarks>
/// <example>
/// <code>
/// public class CustomerQueryHandler(IReadOnlyRepository&lt;Customer, CustomerId&gt; repository)
/// {
///     public async Task&lt;CustomerDto?&gt; Handle(GetCustomerQuery query, CancellationToken ct)
///     {
///         var result = await repository.GetByIdAsync(query.CustomerId, ct);
///
///         return result.Match(
///             success: customer => new CustomerDto(customer),
///             failure: _ => null
///         );
///     }
/// }
/// </code>
/// </example>
public interface IReadOnlyRepository<TEntity, in TId>
    where TEntity : class, IEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// Retrieves an entity by its identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>
    /// <see cref="Result{T}.Success"/> with the entity if found;
    /// <see cref="Result{T}.Failure"/> with <see cref="ErrorCodes.NotFound"/> if not found
    /// (including soft-deleted entities).
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await repository.GetByIdAsync(customerId, ct);
    ///
    /// return result switch
    /// {
    ///     { IsSuccess: true, Value: var customer } => Ok(customer),
    ///     { Error.Code: ErrorCodes.NotFound } => NotFound(),
    ///     _ => Problem()
    /// };
    /// </code>
    /// </example>
    Task<Result<TEntity>> GetByIdAsync(TId id, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a paginated list of entities.
    /// </summary>
    /// <param name="request">Pagination parameters (page number and size).</param>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>
    /// A <see cref="PagedResult{T}"/> containing the requested page of entities
    /// and pagination metadata (total count, total pages, etc.).
    /// </returns>
    /// <remarks>
    /// <para>Results are typically ordered by creation date (newest first) unless
    /// overridden by derived repository methods.</para>
    /// <para>Soft-deleted entities are automatically excluded.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var request = new PagedRequest(Page: 1, PageSize: 20);
    /// var result = await repository.GetPagedAsync(request, ct);
    ///
    /// result.Match(
    ///     success: paged =>
    ///     {
    ///         Console.WriteLine($"Showing {paged.Items.Count} of {paged.TotalCount}");
    ///         foreach (var entity in paged.Items)
    ///         {
    ///             // Process entity
    ///         }
    ///     },
    ///     failure: error => Console.WriteLine(error.Message)
    /// );
    /// </code>
    /// </example>
    Task<Result<PagedResult<TEntity>>> GetPagedAsync(
        PagedRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if an entity with the specified identifier exists.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>
    /// <c>true</c> if the entity exists and is not soft-deleted;
    /// <c>false</c> otherwise.
    /// </returns>
    /// <remarks>
    /// This is more efficient than <see cref="GetByIdAsync"/> when you only need
    /// to check existence without loading the full entity.
    /// </remarks>
    /// <example>
    /// <code>
    /// var existsResult = await repository.ExistsAsync(customerId, ct);
    ///
    /// if (existsResult is { IsSuccess: true, Value: true })
    /// {
    ///     // Entity exists
    /// }
    /// </code>
    /// </example>
    Task<Result<bool>> ExistsAsync(TId id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the total count of non-deleted entities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>The total number of entities (excluding soft-deleted).</returns>
    /// <example>
    /// <code>
    /// var countResult = await repository.CountAsync(ct);
    ///
    /// countResult.Match(
    ///     success: count => Console.WriteLine($"Total: {count} entities"),
    ///     failure: error => Console.WriteLine(error.Message)
    /// );
    /// </code>
    /// </example>
    Task<Result<long>> CountAsync(CancellationToken cancellationToken);
}
