// =============================================================================
// FILE: Documents/MongoDocumentBase.cs
// PURPOSE: Base class for all MongoDB documents with audit fields
// =============================================================================

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Company.Persistence.Mongo.Documents;

/// <summary>
/// Base class for all MongoDB documents with audit and soft delete fields.
/// </summary>
public abstract class MongoDocumentBase
{
    /// <summary>MongoDB internal ObjectId (auto-generated).</summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public ObjectId InternalId { get; set; }

    /// <summary>Domain entity ID stored as string.</summary>
    [BsonElement("id")]
    public required string Id { get; init; }

    // Audit fields
    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTimeOffset CreatedAt { get; set; }

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTimeOffset? UpdatedAt { get; set; }

    [BsonElement("updatedBy")]
    [BsonIgnoreIfNull]
    public string? UpdatedBy { get; set; }

    [BsonElement("version")]
    public long Version { get; set; } = 1;

    // Soft delete fields
    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }

    [BsonElement("deletedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTimeOffset? DeletedAt { get; set; }

    [BsonElement("deletedBy")]
    [BsonIgnoreIfNull]
    public string? DeletedBy { get; set; }
}
