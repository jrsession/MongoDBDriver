// =============================================================================
// FILE: Audit/IAuditHistoryWriter.cs
// PURPOSE: Interface for writing audit history entries
// =============================================================================

using Company.Persistence.Abstractions.Audit;

namespace Company.Persistence.Mongo.Audit;

/// <summary>
/// Writes audit history entries to the audit collection.
/// </summary>
/// <remarks>
/// <para>Registered as a singleton service and injected into repositories.</para>
/// <para>All write operations automatically call this to record changes.</para>
/// </remarks>
public interface IAuditHistoryWriter
{
    /// <summary>
    /// Writes an audit entry for an entity change.
    /// </summary>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="entityId">The entity ID as string.</param>
    /// <param name="action">The action performed.</param>
    /// <param name="userId">The user who performed the action.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    /// <param name="oldValue">The entity state before the change (null for create).</param>
    /// <param name="newValue">The entity state after the change (null for delete).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task WriteAsync(
        string entityType,
        string entityId,
        AuditAction action,
        string userId,
        string? correlationId,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken);
}
