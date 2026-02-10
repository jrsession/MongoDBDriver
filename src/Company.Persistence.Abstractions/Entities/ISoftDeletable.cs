// =============================================================================
// FILE: Entities/ISoftDeletable.cs
// PURPOSE: Soft delete marker interface
// =============================================================================

namespace Company.Persistence.Abstractions.Entities;

/// <summary>
/// Marker interface for entities that support soft deletion.
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> When an entity implements this interface, the repository will:</para>
/// <list type="bullet">
///   <item>Set <see cref="IsDeleted"/> to <c>true</c> instead of physically deleting</item>
///   <item>Automatically filter out soft-deleted entities from all queries</item>
///   <item>Record <see cref="DeletedAt"/> and <see cref="DeletedBy"/> for audit trail</item>
/// </list>
/// <para><b>Why Soft Delete:</b></para>
/// <list type="bullet">
///   <item><b>Audit Trail:</b> Know who deleted what and when</item>
///   <item><b>Recovery:</b> Accidental deletions are reversible</item>
///   <item><b>Compliance:</b> Many regulations require data retention</item>
///   <item><b>Referential Integrity:</b> Foreign references don't break</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// public sealed class Customer : IAuditableEntity&lt;CustomerId&gt;, ISoftDeletable
/// {
///     public CustomerId Id { get; private init; }
///
///     // Soft delete fields - set by repository
///     public bool IsDeleted { get; private set; }
///     public DateTimeOffset? DeletedAt { get; private set; }
///     public string? DeletedBy { get; private set; }
/// }
///
/// // When calling DeleteAsync, the entity is soft-deleted:
/// await repository.DeleteAsync(customerId, cancellationToken);
/// // IsDeleted = true, DeletedAt = now, DeletedBy = current user
///
/// // Subsequent queries automatically exclude soft-deleted entities:
/// var result = await repository.GetByIdAsync(customerId, cancellationToken);
/// // Returns Error.NotFound because entity is soft-deleted
/// </code>
/// </example>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets whether the entity has been soft-deleted.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the entity is excluded from all standard queries.
    /// </remarks>
    bool IsDeleted { get; }

    /// <summary>
    /// Gets when the entity was soft-deleted (UTC), or null if not deleted.
    /// </summary>
    DateTimeOffset? DeletedAt { get; }

    /// <summary>
    /// Gets the user ID who soft-deleted the entity, or null if not deleted.
    /// </summary>
    string? DeletedBy { get; }
}
