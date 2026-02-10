// =============================================================================
// FILE: Audit/AuditEntry.cs
// PURPOSE: Change history record for audit trail
// =============================================================================

namespace Company.Persistence.Abstractions.Audit;

/// <summary>
/// Represents a single change in the audit history.
/// </summary>
/// <remarks>
/// <para><b>Storage:</b> Stored in a separate "_audit_history" collection to:</para>
/// <list type="bullet">
///   <item>Avoid document bloat on main entities</item>
///   <item>Enable TTL indexes for retention policies</item>
///   <item>Keep operational queries fast</item>
/// </list>
/// <para><b>Use Cases:</b></para>
/// <list type="bullet">
///   <item>Compliance and regulatory requirements</item>
///   <item>Debugging data issues</item>
///   <item>Audit reports</item>
///   <item>Undo/restore functionality</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Query audit history for an entity
/// var history = await auditRepository.GetHistoryAsync("Customer", customerId.ToString(), ct);
///
/// foreach (var entry in history)
/// {
///     Console.WriteLine($"{entry.Timestamp}: {entry.Action} by {entry.UserId}");
///
///     if (entry.Action == AuditAction.Updated)
///     {
///         Console.WriteLine($"  Old: {entry.OldValue}");
///         Console.WriteLine($"  New: {entry.NewValue}");
///     }
/// }
/// </code>
/// </example>
public sealed record AuditEntry
{
    /// <summary>
    /// Gets the unique identifier for this audit entry.
    /// </summary>
    /// <remarks>
    /// Auto-generated GUID as string.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the type name of the audited entity.
    /// </summary>
    /// <remarks>
    /// Examples: "Customer", "Order", "Product"
    /// </remarks>
    public required string EntityType { get; init; }

    /// <summary>
    /// Gets the entity's identifier as a string.
    /// </summary>
    /// <remarks>
    /// The strongly-typed ID is converted to string for storage.
    /// </remarks>
    public required string EntityId { get; init; }

    /// <summary>
    /// Gets the action that was performed.
    /// </summary>
    public required AuditAction Action { get; init; }

    /// <summary>
    /// Gets the user ID who performed the action.
    /// </summary>
    /// <remarks>
    /// Resolved from <see cref="IUserContextProvider.GetCurrentUserId"/>.
    /// </remarks>
    public required string UserId { get; init; }

    /// <summary>
    /// Gets when the action was performed (UTC).
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the correlation ID for distributed tracing.
    /// </summary>
    /// <remarks>
    /// <para>Resolved from <see cref="IUserContextProvider.GetCorrelationId"/>.</para>
    /// <para>Use this to correlate audit entries with Application Insights traces.</para>
    /// </remarks>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the entity state before the change.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><b>Created:</b> <c>null</c></item>
    ///   <item><b>Updated:</b> Entity state before the update</item>
    ///   <item><b>Deleted:</b> Entity state before deletion</item>
    /// </list>
    /// Stored as a JSON-serializable object (typically the document representation).
    /// </remarks>
    public object? OldValue { get; init; }

    /// <summary>
    /// Gets the entity state after the change.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><b>Created:</b> The newly created entity</item>
    ///   <item><b>Updated:</b> Entity state after the update</item>
    ///   <item><b>Deleted:</b> <c>null</c></item>
    /// </list>
    /// Stored as a JSON-serializable object (typically the document representation).
    /// </remarks>
    public object? NewValue { get; init; }

    /// <summary>
    /// Creates a new audit entry for an entity creation.
    /// </summary>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="userId">The user who created the entity.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    /// <param name="newValue">The created entity state.</param>
    /// <returns>A new <see cref="AuditEntry"/> for the creation.</returns>
    public static AuditEntry ForCreated(
        string entityType,
        string entityId,
        string userId,
        string? correlationId,
        object newValue) => new()
        {
            Id = Guid.NewGuid().ToString(),
            EntityType = entityType,
            EntityId = entityId,
            Action = AuditAction.Created,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            OldValue = null,
            NewValue = newValue
        };

    /// <summary>
    /// Creates a new audit entry for an entity update.
    /// </summary>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="userId">The user who updated the entity.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    /// <param name="oldValue">The entity state before the update.</param>
    /// <param name="newValue">The entity state after the update.</param>
    /// <returns>A new <see cref="AuditEntry"/> for the update.</returns>
    public static AuditEntry ForUpdated(
        string entityType,
        string entityId,
        string userId,
        string? correlationId,
        object oldValue,
        object newValue) => new()
        {
            Id = Guid.NewGuid().ToString(),
            EntityType = entityType,
            EntityId = entityId,
            Action = AuditAction.Updated,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            OldValue = oldValue,
            NewValue = newValue
        };

    /// <summary>
    /// Creates a new audit entry for an entity deletion.
    /// </summary>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="userId">The user who deleted the entity.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    /// <param name="oldValue">The entity state before deletion.</param>
    /// <returns>A new <see cref="AuditEntry"/> for the deletion.</returns>
    public static AuditEntry ForDeleted(
        string entityType,
        string entityId,
        string userId,
        string? correlationId,
        object oldValue) => new()
        {
            Id = Guid.NewGuid().ToString(),
            EntityType = entityType,
            EntityId = entityId,
            Action = AuditAction.Deleted,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            OldValue = oldValue,
            NewValue = null
        };
}
