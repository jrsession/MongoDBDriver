// =============================================================================
// FILE: Client/MongoClientProvider.cs
// PURPOSE: Singleton MongoClient with Atlas configuration and telemetry
// =============================================================================

using System.Diagnostics;
using Company.Persistence.Mongo.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace Company.Persistence.Mongo.Client;

/// <summary>
/// Provides a singleton MongoClient configured for MongoDB Atlas.
/// </summary>
public sealed class MongoClientProvider : IMongoClientProvider, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("Company.Persistence.Mongo", "1.0.0");

    private readonly Lazy<IMongoClient> _client;
    private readonly MongoSettings _settings;
    private readonly ILogger<MongoClientProvider> _logger;
    private bool _disposed;

    public MongoClientProvider(IOptions<MongoSettings> settings, ILogger<MongoClientProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings.Value ?? throw new ArgumentException("MongoDB settings not configured", nameof(settings));
        _logger = logger;
        _client = new Lazy<IMongoClient>(CreateClient, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IMongoClient Client => _client.Value;
    public IMongoDatabase Database => Client.GetDatabase(_settings.DatabaseName);

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        return Database.GetCollection<TDocument>(collectionName);
    }

    private IMongoClient CreateClient()
    {
        _logger.LogInformation("Creating MongoDB client for '{Database}'", _settings.DatabaseName);

        var mongoUrl = new MongoUrl(_settings.ConnectionString);
        var clientSettings = MongoClientSettings.FromUrl(mongoUrl);

        // Security
        clientSettings.UseTls = true;
        clientSettings.AllowInsecureTls = false;

        // Timeouts
        clientSettings.ConnectTimeout = TimeSpan.FromMilliseconds(_settings.ConnectTimeoutMs);
        clientSettings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(_settings.ServerSelectionTimeoutMs);
        clientSettings.SocketTimeout = TimeSpan.FromMilliseconds(_settings.SocketTimeoutMs);

        // Connection pool
        clientSettings.MaxConnectionPoolSize = _settings.MaxConnectionPoolSize;
        clientSettings.MinConnectionPoolSize = _settings.MinConnectionPoolSize;

        // Read/Write settings
        clientSettings.ReadPreference = ParseReadPreference(_settings.ReadPreference);
        clientSettings.WriteConcern = ParseWriteConcern(_settings.WriteConcern);
        clientSettings.ReadConcern = ParseReadConcern(_settings.ReadConcern);
        clientSettings.RetryWrites = _settings.RetryWrites;
        clientSettings.RetryReads = _settings.RetryReads;

        // Observability
        clientSettings.ClusterConfigurator = ConfigureObservability;

        _logger.LogInformation("MongoDB client created. Pool: {Min}-{Max}",
            _settings.MinConnectionPoolSize, _settings.MaxConnectionPoolSize);

        return new MongoClient(clientSettings);
    }

    private void ConfigureObservability(MongoDB.Driver.Core.Configuration.ClusterBuilder clusterBuilder)
    {
        var activities = new System.Collections.Concurrent.ConcurrentDictionary<int, Activity>();

        clusterBuilder.Subscribe<CommandStartedEvent>(e =>
        {
            if (!ShouldTrace(e.CommandName)) return;

            var activity = ActivitySource.StartActivity($"MongoDB {e.CommandName}", ActivityKind.Client);
            if (activity is null) return;

            activity.SetTag("db.system", "mongodb");
            activity.SetTag("db.name", e.DatabaseNamespace?.DatabaseName);
            activity.SetTag("db.operation", e.CommandName);
            activities[e.RequestId] = activity;

            _logger.LogDebug("MongoDB {Command} started", e.CommandName);
        });

        clusterBuilder.Subscribe<CommandSucceededEvent>(e =>
        {
            if (activities.TryRemove(e.RequestId, out var activity))
            {
                activity.SetStatus(ActivityStatusCode.Ok);
                activity.Dispose();
            }
            _logger.LogDebug("MongoDB {Command} succeeded in {Ms:F2}ms", e.CommandName, e.Duration.TotalMilliseconds);
        });

        clusterBuilder.Subscribe<CommandFailedEvent>(e =>
        {
            if (activities.TryRemove(e.RequestId, out var activity))
            {
                activity.SetStatus(ActivityStatusCode.Error, e.Failure?.Message);
                if (e.Failure is not null)
                {
                    activity.SetTag("exception.type", e.Failure.GetType().FullName);
                    activity.SetTag("exception.message", e.Failure.Message);
                }
                activity.Dispose();
            }
            _logger.LogWarning(e.Failure, "MongoDB {Command} failed after {Ms:F2}ms", e.CommandName, e.Duration.TotalMilliseconds);
        });
    }

    private static bool ShouldTrace(string cmd) => cmd switch
    {
        "isMaster" or "hello" or "ping" or "buildInfo" or "getLastError"
            or "saslStart" or "saslContinue" or "ismaster" => false,
        _ => true
    };

    private static MongoDB.Driver.ReadPreference ParseReadPreference(string v) => v.ToLowerInvariant() switch
    {
        "primary" => MongoDB.Driver.ReadPreference.Primary,
        "primarypreferred" => MongoDB.Driver.ReadPreference.PrimaryPreferred,
        "secondary" => MongoDB.Driver.ReadPreference.Secondary,
        "secondarypreferred" => MongoDB.Driver.ReadPreference.SecondaryPreferred,
        "nearest" => MongoDB.Driver.ReadPreference.Nearest,
        _ => MongoDB.Driver.ReadPreference.SecondaryPreferred
    };

    private static MongoDB.Driver.WriteConcern ParseWriteConcern(string v) => v.ToLowerInvariant() switch
    {
        "0" or "unacknowledged" => MongoDB.Driver.WriteConcern.Unacknowledged,
        "1" => MongoDB.Driver.WriteConcern.W1,
        _ => MongoDB.Driver.WriteConcern.WMajority
    };

    private static MongoDB.Driver.ReadConcern ParseReadConcern(string v) => v.ToLowerInvariant() switch
    {
        "local" => MongoDB.Driver.ReadConcern.Local,
        "linearizable" => MongoDB.Driver.ReadConcern.Linearizable,
        "snapshot" => MongoDB.Driver.ReadConcern.Snapshot,
        _ => MongoDB.Driver.ReadConcern.Majority
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_client.IsValueCreated)
        {
            _client.Value.Cluster.Dispose();
            _logger.LogInformation("MongoDB client disposed");
        }
    }
}
