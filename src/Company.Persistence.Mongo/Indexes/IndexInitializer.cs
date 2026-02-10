// =============================================================================
// FILE: Indexes/IndexInitializer.cs
// PURPOSE: Hosted service that creates indexes at application startup
// =============================================================================

using Company.Persistence.Mongo.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Company.Persistence.Mongo.Indexes;

/// <summary>
/// Background service that creates MongoDB indexes at application startup.
/// </summary>
/// <remarks>
/// <para><b>Design Decision:</b> Uses <see cref="IHostedService"/> to run after DI is configured
/// but before the application starts accepting requests.</para>
/// <para><b>Idempotent:</b> Index creation is safe to run multiple times. Existing indexes
/// with the same name are skipped.</para>
/// <para><b>Best Practice:</b> Indexes are created in the background to not block startup.
/// For production, consider creating indexes during deployment instead.</para>
/// </remarks>
/// <example>
/// Registration:
/// <code>
/// // Automatically registered when using AddMongoPersistence()
/// // Or register manually:
/// services.AddHostedService&lt;IndexInitializer&gt;();
/// services.AddSingleton&lt;IIndexDefinitionProvider&lt;CustomerDocument&gt;, CustomerIndexProvider&gt;();
/// </code>
/// </example>
public sealed class IndexInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexInitializer> _logger;

    /// <summary>
    /// Creates a new index initializer.
    /// </summary>
    public IndexInitializer(
        IServiceProvider serviceProvider,
        ILogger<IndexInitializer> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MongoDB index initialization");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var registrations = scope.ServiceProvider.GetServices<IndexRegistration>();

            foreach (var registration in registrations)
            {
                await registration.CreateIndexesAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("MongoDB index initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MongoDB indexes");
            // Don't throw - allow app to start even if index creation fails
            // Indexes can be created manually or on next restart
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Internal registration for index creation. Used by the DI system to track
/// which collections need indexes.
/// </summary>
internal abstract class IndexRegistration
{
    public abstract Task CreateIndexesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Typed index registration that connects a document type to its index provider.
/// </summary>
/// <typeparam name="TDocument">The MongoDB document type.</typeparam>
internal sealed class IndexRegistration<TDocument> : IndexRegistration
{
    private readonly IMongoClientProvider _clientProvider;
    private readonly IIndexDefinitionProvider<TDocument> _indexProvider;
    private readonly string _collectionName;
    private readonly ILogger _logger;

    public IndexRegistration(
        IMongoClientProvider clientProvider,
        IIndexDefinitionProvider<TDocument> indexProvider,
        string collectionName,
        ILogger<IndexRegistration<TDocument>> logger)
    {
        _clientProvider = clientProvider;
        _indexProvider = indexProvider;
        _collectionName = collectionName;
        _logger = logger;
    }

    public override async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        var collection = _clientProvider.GetCollection<TDocument>(_collectionName);
        var indexes = _indexProvider.GetIndexes().ToList();

        if (indexes.Count == 0)
        {
            _logger.LogDebug("No indexes defined for collection {CollectionName}", _collectionName);
            return;
        }

        _logger.LogInformation(
            "Creating {IndexCount} indexes for collection {CollectionName}",
            indexes.Count,
            _collectionName);

        try
        {
            await collection.Indexes
                .CreateManyAsync(indexes, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully created indexes for collection {CollectionName}",
                _collectionName);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict")
        {
            _logger.LogWarning(
                "Index options conflict in collection {CollectionName}. " +
                "Existing indexes may have different options. Error: {Message}",
                _collectionName,
                ex.Message);
        }
    }
}
