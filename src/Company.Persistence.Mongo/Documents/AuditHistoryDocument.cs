// =============================================================================
// FILE: Documents/AuditHistoryDocument.cs
// PURPOSE: MongoDB document for audit history entries (INTERNAL)
// =============================================================================

using Company.Persistence.Abstractions.Audit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Company.Persistence.Mongo.Documents;

/// <summary>
/// MongoDB document for storing audit history entries.
/// </summary>
/// <remarks>
/// <para><b>Collection:</b> Stored in the "_audit_history" collection (configurable).</para>
/// <para><b>Indexes:</b> Should have indexes on (EntityType, EntityId, Timestamp) for efficient queries.</para>
/// <para><b>TTL:</b> Consider adding a TTL index on Timestamp for automatic cleanup.</para>
/// </remarks>
internal sealed class AuditHistoryDocument
{
    /// <summary>
    /// Gets or sets the unique identifier for this audit entry.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the type name of the audited entity.
    /// </summary>
    [BsonElement("entityType")]
    public required string EntityType { get; init; }

    /// <summary>
    /// Gets or sets the entity's identifier as a string.
    /// </summary>
    [BsonElement("entityId")]
    public required string EntityId { get; init; }

    /// <summary>
    /// Gets or sets the action that was performed.
    /// </summary>
    [BsonElement("action")]
    [BsonRepresentation(BsonType.String)]
    public required AuditAction Action { get; init; }

    /// <summary>
    /// Gets or sets the user ID who performed the action.
    /// </summary>
    [BsonElement("userId")]
    public required string UserId { get; init; }

    /// <summary>
    /// Gets or sets when the action was performed (UTC).
    /// </summary>
    [BsonElement("timestamp")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    [BsonElement("correlationId")]
    [BsonIgnoreIfNull]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the entity state before the change.
    /// </summary>
    /// <remarks>
    /// Stored as raw BSON document for flexibility.
    /// </remarks>
    [BsonElement("oldValue")]
    [BsonIgnoreIfNull]
    public BsonDocument? OldValue { get; init; }

    /// <summary>
    /// Gets or sets the entity state after the change.
    /// </summary>
    [BsonElement("newValue")]
    [BsonIgnoreIfNull]
    public BsonDocument? NewValue { get; init; }
}
