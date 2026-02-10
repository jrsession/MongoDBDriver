// =============================================================================
// FILE: Repositories/IRepository.cs
// PURPOSE: Full CRUD repository interface
// =============================================================================

using Company.Persistence.Abstractions.Entities;
using Company.Persistence.Abstractions.Results;

namespace Company.Persistence.Abstractions.Repositories;

/// <summary>
/// Full repository interface for CRUD operations.
/// </summary>
/// <typeparam name="TEntity">The domain entity type.</typeparam>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <remarks>
/// <para><b>Extends:</b> <see cref="IReadOnlyRepository{TEntity,TId}"/> to include all read operations.</para>
/// <para><b>Audit:</b> All mutation operations automatically populate audit fields
/// (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) using <see cref="Audit.IUserContextProvider"/>.</para>
/// <para><b>Concurrency:</b> <see cref="UpdateAsync"/> uses optimistic concurrency via the
/// <see cref="IAuditableEntity{TId}.Version"/> field. If the entity was modified since loading,
/// the update fails with <see cref="ErrorCodes.Conflict"/>.</para>
/// <para><b>Soft Delete:</b> <see cref="DeleteAsync"/> performs soft deletion for entities
/// implementing <see cref="ISoftDeletable"/>, setting IsDeleted=true rather than physically
/// removing the document.</para>
/// </remarks>
/// <example>
/// <code>
/// public class CustomerService(IRepository&lt;Customer, CustomerId&gt; repository)
/// {
///     public async Task&lt;Result&lt;Customer&gt;&gt; CreateAsync(
///         CreateCustomerCommand cmd,
///         CancellationToken ct)
///     {
///         var customer = Customer.Create(cmd.Name, cmd.Email);
///         return await repository.AddAsync(customer, ct);
///         // CreatedAt, CreatedBy, Version automatically set
///     }
///
///     public async Task&lt;Result&lt;Customer&gt;&gt; UpdateEmailAsync(
///         CustomerId id,
///         string newEmail,
///         CancellationToken ct)
///     {
///         var result = await repository.GetByIdAsync(id, ct);
///
///         return await result.BindAsync(async customer =>
///         {
///             customer.UpdateEmail(newEmail);
///             return await repository.UpdateAsync(customer, ct);
///             // UpdatedAt, UpdatedBy, Version automatically updated
///         });
///     }
/// }
/// </code>
/// </example>
public interface IRepository<TEntity, in TId> : IReadOnlyRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>
    /// The added entity with populated audit fields:
    /// <list type="bullet">
    ///   <item><b>CreatedAt:</b> Set to current UTC time</item>
    ///   <item><b>CreatedBy:</b> Set to current user ID</item>
    ///   <item><b>Version:</b> Set to 1</item>
    /// </list>
    /// Returns <see cref="ErrorCodes.Duplicate"/> if a unique constraint is violated.
    /// </returns>
    /// <remarks>
    /// <para>The entity's ID should already be set before calling this method.</para>
    /// <para>Audit history is automatically recorded.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var customer = Customer.Create(name, email);
    /// var result = await repository.AddAsync(customer, ct);
    ///
    /// return result.Match(
    ///     success: created => CreatedAtAction(nameof(GetById), new { id = created.Id }, created),
    ///     failure: error => error.Code switch
    ///     {
    ///         ErrorCodes.Duplicate => Conflict(error.Message),
    ///         _ => Problem(error.Message)
    ///     }
    /// );
    /// </code>
    /// </example>
    Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>
    /// The updated entity with populated audit fields:
    /// <list type="bullet">
    ///   <item><b>UpdatedAt:</b> Set to current UTC time</item>
    ///   <item><b>UpdatedBy:</b> Set to current user ID</item>
    ///   <item><b>Version:</b> Incremented by 1</item>
    /// </list>
    /// Returns <see cref="ErrorCodes.NotFound"/> if the entity doesn't exist.
    /// Returns <see cref="ErrorCodes.Conflict"/> if the version has changed (concurrent modification).
    /// </returns>
    /// <remarks>
    /// <para><b>Optimistic Concurrency:</b> The entity's current <see cref="IAuditableEntity{TId}.Version"/>
    /// is compared against the database. If they don't match, another process modified the entity
    /// and the update is rejected with <see cref="ErrorCodes.Conflict"/>.</para>
    /// <para><b>Handling Conflicts:</b> The caller should either:</para>
    /// <list type="bullet">
    ///   <item>Reload the entity and retry the update</item>
    ///   <item>Inform the user that the data has changed</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Load, modify, and update
    /// var getResult = await repository.GetByIdAsync(id, ct);
    /// if (!getResult.IsSuccess) return getResult;
    ///
    /// var customer = getResult.Value;
    /// customer.UpdateEmail(newEmail);
    ///
    /// var updateResult = await repository.UpdateAsync(customer, ct);
    ///
    /// return updateResult.Match(
    ///     success: updated => Ok(updated),
    ///     failure: error => error.Code switch
    ///     {
    ///         ErrorCodes.NotFound => NotFound(),
    ///         ErrorCodes.Conflict => Conflict("Customer was modified by another user. Please refresh."),
    ///         _ => Problem(error.Message)
    ///     }
    /// );
    /// </code>
    /// </example>
    Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete.</param>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>
    /// <see cref="Unit"/> on success.
    /// Returns <see cref="ErrorCodes.NotFound"/> if the entity doesn't exist or is already deleted.
    /// </returns>
    /// <remarks>
    /// <para><b>Soft Delete:</b> For entities implementing <see cref="ISoftDeletable"/>,
    /// this performs a soft delete:</para>
    /// <list type="bullet">
    ///   <item><b>IsDeleted:</b> Set to <c>true</c></item>
    ///   <item><b>DeletedAt:</b> Set to current UTC time</item>
    ///   <item><b>DeletedBy:</b> Set to current user ID</item>
    /// </list>
    /// <para>The document remains in the database but is excluded from all queries.</para>
    /// <para>Audit history is automatically recorded.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var result = await repository.DeleteAsync(customerId, ct);
    ///
    /// return result.Match(
    ///     success: _ => NoContent(),
    ///     failure: error => error.Code switch
    ///     {
    ///         ErrorCodes.NotFound => NotFound(),
    ///         _ => Problem(error.Message)
    ///     }
    /// );
    /// </code>
    /// </example>
    Task<Result<Unit>> DeleteAsync(TId id, CancellationToken cancellationToken);
}
