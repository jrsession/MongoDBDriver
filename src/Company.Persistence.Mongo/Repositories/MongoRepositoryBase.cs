// =============================================================================
// FILE: Repositories/MongoRepositoryBase.cs
// PURPOSE: Base repository with CRUD, audit, soft delete, and concurrency
// =============================================================================

using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Company.Persistence.Abstractions.Audit;
using Company.Persistence.Abstractions.Entities;
using Company.Persistence.Abstractions.Paging;
using Company.Persistence.Abstractions.Repositories;
using Company.Persistence.Abstractions.Results;
using Company.Persistence.Mongo.Audit;
using Company.Persistence.Mongo.Client;
using Company.Persistence.Mongo.Documents;
using Company.Persistence.Mongo.Mapping;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Company.Persistence.Mongo.Repositories;

/// <summary>
/// Base repository implementation with audit, soft delete, and optimistic concurrency.
/// </summary>
/// <typeparam name="TEntity">The domain entity type.</typeparam>
/// <typeparam name="TId">The strongly-typed ID type.</typeparam>
/// <typeparam name="TDocument">The MongoDB document type.</typeparam>
/// <remarks>
/// <para><b>Design Decision:</b> Abstract class provides complete CRUD implementation.
/// Derived repositories only add domain-specific queries.</para>
/// <para><b>Features:</b></para>
/// <list type="bullet">
///   <item>Automatic audit field population (CreatedAt, UpdatedAt, etc.)</item>
///   <item>Optimistic concurrency via Version field</item>
///   <item>Soft delete with automatic query filtering</item>
///   <item>Audit history recording</item>
///   <item>OpenTelemetry Activity tracking</item>
/// </list>
/// </remarks>
public abstract class MongoRepositoryBase<TEntity, TId, TDocument> : IRepository<TEntity, TId>
    where TEntity : class, IAuditableEntity<TId>, ISoftDeletable
    where TId : notnull
    where TDocument : MongoDocumentBase
{
    private readonly IMongoCollection<TDocument> _collection;
    private readonly IEntityMapper<TEntity, TId, TDocument> _mapper;
    private readonly IUserContextProvider _userContext;
    private readonly IAuditHistoryWriter _auditWriter;
    private readonly ILogger _logger;
    private readonly string _entityTypeName;

    /// <summary>
    /// Filter that excludes soft-deleted documents.
    /// </summary>
    protected static readonly FilterDefinition<TDocument> NotDeletedFilter =
        Builders<TDocument>.Filter.Eq(d => d.IsDeleted, false);

    /// <summary>
    /// Creates a new repository instance.
    /// </summary>
    protected MongoRepositoryBase(
        IMongoClientProvider clientProvider,
        IEntityMapper<TEntity, TId, TDocument> mapper,
        IUserContextProvider userContext,
        IAuditHistoryWriter auditWriter,
        ILogger logger,
        string collectionName)
    {
        ArgumentNullException.ThrowIfNull(clientProvider);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(auditWriter);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        _collection = clientProvider.GetCollection<TDocument>(collectionName);
        _mapper = mapper;
        _userContext = userContext;
        _auditWriter = auditWriter;
        _logger = logger;
        _entityTypeName = typeof(TEntity).Name;
    }

    /// <summary>
    /// Gets the underlying MongoDB collection for custom queries.
    /// </summary>
    protected IMongoCollection<TDocument> Collection => _collection;

    /// <summary>
    /// Gets the entity mapper.
    /// </summary>
    protected IEntityMapper<TEntity, TId, TDocument> Mapper => _mapper;

    // =========================================================================
    // READ OPERATIONS
    // =========================================================================

    /// <inheritdoc />
    public virtual async Task<Result<TEntity>> GetByIdAsync(TId id, CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        try
        {
            var stringId = _mapper.FormatId(id);
            var filter = Builders<TDocument>.Filter.And(
                Builders<TDocument>.Filter.Eq(d => d.Id, stringId),
                NotDeletedFilter);

            var document = await _collection
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
            {
                _logger.LogDebug("{EntityType} with ID {Id} not found", _entityTypeName, id);
                return Error.NotFound(_entityTypeName, id);
            }

            return _mapper.ToDomain(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {EntityType} with ID {Id}", _entityTypeName, id);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<PagedResult<TEntity>>> GetPagedAsync(
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        return await GetPagedInternalAsync(NotDeletedFilter, request, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<Result<bool>> ExistsAsync(TId id, CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        try
        {
            var stringId = _mapper.FormatId(id);
            var filter = Builders<TDocument>.Filter.And(
                Builders<TDocument>.Filter.Eq(d => d.Id, stringId),
                NotDeletedFilter);

            var count = await _collection
                .CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken)
                .ConfigureAwait(false);

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of {EntityType} with ID {Id}", _entityTypeName, id);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<long>> CountAsync(CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        try
        {
            var count = await _collection
                .CountDocumentsAsync(NotDeletedFilter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting {EntityType}", _entityTypeName);
            throw;
        }
    }

    // =========================================================================
    // WRITE OPERATIONS
    // =========================================================================

    /// <inheritdoc />
    public virtual async Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        try
        {
            var now = DateTimeOffset.UtcNow;
            var userId = _userContext.GetCurrentUserId();

            var document = _mapper.ToDocument(entity);
            document.CreatedAt = now;
            document.CreatedBy = userId;
            document.Version = 1;
            document.IsDeleted = false;

            await _collection
                .InsertOneAsync(document, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Write audit history (fire and forget pattern - don't await in critical path)
            _ = _auditWriter.WriteAsync(
                _entityTypeName,
                document.Id,
                AuditAction.Created,
                userId,
                _userContext.GetCorrelationId(),
                oldValue: null,
                newValue: document,
                cancellationToken);

            _logger.LogInformation(
                "Created {EntityType} with ID {Id} by {UserId}",
                _entityTypeName, document.Id, userId);

            return _mapper.ToDomain(document);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning("Duplicate key error creating {EntityType}: {Message}", _entityTypeName, ex.Message);
            return Error.Duplicate($"A {_entityTypeName} with the same key already exists.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating {EntityType}", _entityTypeName);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        try
        {
            var stringId = _mapper.FormatId(entity.Id);
            var now = DateTimeOffset.UtcNow;
            var userId = _userContext.GetCurrentUserId();
            var expectedVersion = entity.Version;

            // Fetch old document for audit history
            var oldDocument = await _collection
                .Find(Builders<TDocument>.Filter.Eq(d => d.Id, stringId))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldDocument is null)
            {
                return Error.NotFound(_entityTypeName, entity.Id);
            }

            if (oldDocument.IsDeleted)
            {
                return Error.NotFound(_entityTypeName, entity.Id);
            }

            // Optimistic concurrency check
            var filter = Builders<TDocument>.Filter.And(
                Builders<TDocument>.Filter.Eq(d => d.Id, stringId),
                Builders<TDocument>.Filter.Eq(d => d.Version, expectedVersion),
                NotDeletedFilter);

            var newDocument = _mapper.ToDocument(entity);
            newDocument.InternalId = oldDocument.InternalId; // Preserve MongoDB _id
            newDocument.CreatedAt = oldDocument.CreatedAt;   // Preserve creation audit
            newDocument.CreatedBy = oldDocument.CreatedBy;
            newDocument.UpdatedAt = now;
            newDocument.UpdatedBy = userId;
            newDocument.Version = expectedVersion + 1;

            var result = await _collection
                .ReplaceOneAsync(filter, newDocument, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (result.ModifiedCount == 0)
            {
                _logger.LogWarning(
                    "Concurrency conflict updating {EntityType} with ID {Id}. Expected version {ExpectedVersion}",
                    _entityTypeName, entity.Id, expectedVersion);

                return Error.ConcurrencyConflict(_entityTypeName, entity.Id);
            }

            // Write audit history
            _ = _auditWriter.WriteAsync(
                _entityTypeName,
                stringId,
                AuditAction.Updated,
                userId,
                _userContext.GetCorrelationId(),
                oldValue: oldDocument,
                newValue: newDocument,
                cancellationToken);

            _logger.LogInformation(
                "Updated {EntityType} with ID {Id} to version {Version} by {UserId}",
                _entityTypeName, stringId, newDocument.Version, userId);

            return _mapper.ToDomain(newDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating {EntityType} with ID {Id}", _entityTypeName, entity.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<Unit>> DeleteAsync(TId id, CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        try
        {
            var stringId = _mapper.FormatId(id);
            var now = DateTimeOffset.UtcNow;
            var userId = _userContext.GetCurrentUserId();

            // Fetch document for audit history
            var filter = Builders<TDocument>.Filter.And(
                Builders<TDocument>.Filter.Eq(d => d.Id, stringId),
                NotDeletedFilter);

            var oldDocument = await _collection
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldDocument is null)
            {
                return Error.NotFound(_entityTypeName, id);
            }

            // Soft delete: set flags instead of removing
            var update = Builders<TDocument>.Update
                .Set(d => d.IsDeleted, true)
                .Set(d => d.DeletedAt, now)
                .Set(d => d.DeletedBy, userId);

            await _collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Write audit history
            _ = _auditWriter.WriteAsync(
                _entityTypeName,
                stringId,
                AuditAction.Deleted,
                userId,
                _userContext.GetCorrelationId(),
                oldValue: oldDocument,
                newValue: null,
                cancellationToken);

            _logger.LogInformation(
                "Soft-deleted {EntityType} with ID {Id} by {UserId}",
                _entityTypeName, stringId, userId);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting {EntityType} with ID {Id}", _entityTypeName, id);
            throw;
        }
    }

    // =========================================================================
    // PROTECTED HELPERS FOR DERIVED REPOSITORIES
    // =========================================================================

    /// <summary>
    /// Gets a single entity matching the filter.
    /// </summary>
    protected async Task<Result<TEntity>> GetSingleAsync(
        FilterDefinition<TDocument> filter,
        CancellationToken cancellationToken)
    {
        var combinedFilter = Builders<TDocument>.Filter.And(filter, NotDeletedFilter);

        var document = await _collection
            .Find(combinedFilter)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            return Error.NotFound(_entityTypeName, "matching filter");
        }

        return _mapper.ToDomain(document);
    }

    /// <summary>
    /// Gets a paged result with a custom filter.
    /// </summary>
    protected async Task<Result<PagedResult<TEntity>>> GetPagedAsync(
        FilterDefinition<TDocument> filter,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        return await GetPagedInternalAsync(filter, request, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a paged result with a custom filter and sort.
    /// </summary>
    protected async Task<Result<PagedResult<TEntity>>> GetPagedAsync<TKey>(
        FilterDefinition<TDocument> filter,
        PagedRequest request,
        Expression<Func<TDocument, TKey>> sortBy,
        CancellationToken cancellationToken,
        bool descending = false)
    {
        // Create sort definition from expression
        SortDefinition<TDocument> sort = descending
            ? Builders<TDocument>.Sort.Descending(new ExpressionFieldDefinition<TDocument, TKey>(sortBy))
            : Builders<TDocument>.Sort.Ascending(new ExpressionFieldDefinition<TDocument, TKey>(sortBy));

        return await GetPagedInternalAsync(filter, request, sort, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<PagedResult<TEntity>>> GetPagedInternalAsync(
        FilterDefinition<TDocument> filter,
        PagedRequest request,
        SortDefinition<TDocument>? sort,
        CancellationToken cancellationToken)
    {
        using var activity = StartActivity();

        try
        {
            // Combine with soft-delete filter
            var combinedFilter = Builders<TDocument>.Filter.And(filter, NotDeletedFilter);

            // Execute count and find in parallel
            var countTask = _collection.CountDocumentsAsync(
                combinedFilter,
                cancellationToken: cancellationToken);

            var findOptions = new FindOptions<TDocument>
            {
                Skip = request.Skip,
                Limit = request.PageSize,
                Sort = sort ?? Builders<TDocument>.Sort.Descending(d => d.CreatedAt)
            };

            var cursor = await _collection
                .FindAsync(combinedFilter, findOptions, cancellationToken)
                .ConfigureAwait(false);

            var documents = await cursor
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var totalCount = await countTask.ConfigureAwait(false);

            var items = documents
                .Select(_mapper.ToDomain)
                .ToList();

            return new PagedResult<TEntity>(items, request.Page, request.PageSize, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged {EntityType}", _entityTypeName);
            throw;
        }
    }

    /// <summary>
    /// Starts an Activity for observability.
    /// </summary>
    private Activity? StartActivity([CallerMemberName] string? operationName = null)
    {
        return Activity.Current?.Source.StartActivity(
            $"{_entityTypeName}.{operationName}",
            ActivityKind.Internal);
    }
}
