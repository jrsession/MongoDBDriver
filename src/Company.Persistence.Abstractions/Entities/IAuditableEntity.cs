// =============================================================================
// FILE: Entities/IAuditableEntity.cs
// PURPOSE: Audit tracking contract with optimistic concurrency
// =============================================================================

namespace Company.Persistence.Abstractions.Entities;

/// <summary>
/// Contract for entities with full audit trail and optimistic concurrency.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <remarks>
/// <para><b>Audit Fields:</b> Automatically populated by the repository layer.
/// Domain code should NOT set these values directly.</para>
/// <para><b>Version Field:</b> Used for optimistic concurrency. Updates will fail
/// with <see cref="Results.ErrorCodes.Conflict"/> if the version has changed since
/// the entity was loaded.</para>
/// <para><b>Implementation Note:</b> Implementing classes should use <c>private set</c>
/// or <c>init</c> accessors to allow the repository to set these values while
/// preventing external modification.</para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class Customer : IAuditableEntity&lt;CustomerId&gt;
/// {
///     public CustomerId Id { get; private init; }
///     public string Name { get; private set; } = string.Empty;
///
///     // Audit fields - set by repository
///     public DateTimeOffset CreatedAt { get; private set; }
///     public string CreatedBy { get; private set; } = string.Empty;
///     public DateTimeOffset? UpdatedAt { get; private set; }
///     public string? UpdatedBy { get; private set; }
///     public long Version { get; private set; }
///
///     public static Customer Create(string name) => new()
///     {
///         Id = CustomerId.New(),
///         Name = name
///     };
/// }
/// </code>
/// </example>
public interface IAuditableEntity<out TId> : IEntity<TId> where TId : notnull
{
    /// <summary>
    /// Gets when the entity was created (UTC).
    /// </summary>
    /// <remarks>
    /// Set automatically by the repository during <c>AddAsync</c>.
    /// </remarks>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the user ID who created the entity.
    /// </summary>
    /// <remarks>
    /// Resolved from <see cref="Audit.IUserContextProvider.GetCurrentUserId"/>.
    /// </remarks>
    string CreatedBy { get; }

    /// <summary>
    /// Gets when the entity was last updated (UTC), or null if never updated.
    /// </summary>
    /// <remarks>
    /// Set automatically by the repository during <c>UpdateAsync</c>.
    /// </remarks>
    DateTimeOffset? UpdatedAt { get; }

    /// <summary>
    /// Gets the user ID who last updated the entity, or null if never updated.
    /// </summary>
    string? UpdatedBy { get; }

    /// <summary>
    /// Gets the optimistic concurrency version number.
    /// </summary>
    /// <remarks>
    /// <para>Starts at 1 when created, increments by 1 on each update.</para>
    /// <para>Used for optimistic concurrency control. If the version in the database
    /// doesn't match the expected version, the update fails with
    /// <see cref="Results.ErrorCodes.Conflict"/>.</para>
    /// </remarks>
    long Version { get; }
}
