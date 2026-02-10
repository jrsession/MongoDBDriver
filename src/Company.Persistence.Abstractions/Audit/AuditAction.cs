// =============================================================================
// FILE: Audit/AuditAction.cs
// PURPOSE: Enumeration of auditable actions
// =============================================================================

namespace Company.Persistence.Abstractions.Audit;

/// <summary>
/// The type of action recorded in an audit entry.
/// </summary>
/// <remarks>
/// Used in <see cref="AuditEntry"/> to indicate what operation was performed.
/// </remarks>
public enum AuditAction
{
    /// <summary>
    /// A new entity was created.
    /// </summary>
    /// <remarks>
    /// <see cref="AuditEntry.OldValue"/> will be <c>null</c>.
    /// <see cref="AuditEntry.NewValue"/> contains the created entity.
    /// </remarks>
    Created = 1,

    /// <summary>
    /// An existing entity was updated.
    /// </summary>
    /// <remarks>
    /// <see cref="AuditEntry.OldValue"/> contains the entity before the update.
    /// <see cref="AuditEntry.NewValue"/> contains the entity after the update.
    /// </remarks>
    Updated = 2,

    /// <summary>
    /// An entity was deleted (soft or hard).
    /// </summary>
    /// <remarks>
    /// <see cref="AuditEntry.OldValue"/> contains the deleted entity.
    /// <see cref="AuditEntry.NewValue"/> will be <c>null</c>.
    /// </remarks>
    Deleted = 3
}
