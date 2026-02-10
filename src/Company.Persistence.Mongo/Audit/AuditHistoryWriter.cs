// =============================================================================
// FILE: Audit/AuditHistoryWriter.cs
// PURPOSE: Implementation for writing audit history entries
// =============================================================================

using Company.Persistence.Abstractions.Audit;
using Company.Persistence.Mongo.Client;
using Company.Persistence.Mongo.Configuration;
using Company.Persistence.Mongo.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Company.Persistence.Mongo.Audit;

/// <summary>
/// Writes audit history entries to the audit collection.
/// </summary>
internal sealed class AuditHistoryWriter : IAuditHistoryWriter
{
    private readonly IMongoCollection<AuditHistoryDocument> _collection;
    private readonly ILogger<AuditHistoryWriter> _logger;

    public AuditHistoryWriter(
        IMongoClientProvider clientProvider,
        IOptions<MongoSettings> settings,
        ILogger<AuditHistoryWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(clientProvider);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        var collectionName = settings.Value.AuditHistoryCollectionName;
        _collection = clientProvider.GetCollection<AuditHistoryDocument>(collectionName);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        string entityType,
        string entityId,
        AuditAction action,
        string userId,
        string? correlationId,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = new AuditHistoryDocument
            {
                Id = Guid.NewGuid().ToString(),
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                UserId = userId,
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
                OldValue = SerializeToBson(oldValue),
                NewValue = SerializeToBson(newValue)
            };

            await _collection
                .InsertOneAsync(document, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogDebug(
                "Audit entry written: {Action} on {EntityType}/{EntityId} by {UserId}",
                action, entityType, entityId, userId);
        }
        catch (Exception ex)
        {
            // Log but don't fail the main operation if audit write fails
            _logger.LogError(
                ex,
                "Failed to write audit entry for {Action} on {EntityType}/{EntityId}",
                action, entityType, entityId);
        }
    }

    private static BsonDocument? SerializeToBson(object? value)
    {
        if (value is null) return null;
        if (value is BsonDocument bson) return bson;

        try
        {
            return value.ToBsonDocument();
        }
        catch
        {
            // If serialization fails, try as JSON
            return BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(value));
        }
    }
}
