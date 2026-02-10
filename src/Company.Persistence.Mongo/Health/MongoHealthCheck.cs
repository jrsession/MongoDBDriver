// =============================================================================
// FILE: Health/MongoHealthCheck.cs
// PURPOSE: Health check for MongoDB connectivity
// =============================================================================

using System.Diagnostics;
using Company.Persistence.Mongo.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Company.Persistence.Mongo.Health;

/// <summary>
/// Health check that verifies MongoDB connectivity via ping command.
/// </summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClientProvider _clientProvider;
    private readonly ILogger<MongoHealthCheck> _logger;

    public MongoHealthCheck(IMongoClientProvider clientProvider, ILogger<MongoHealthCheck> logger)
    {
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var pingCommand = new BsonDocument("ping", 1);
            await _clientProvider.Database
                .RunCommandAsync<BsonDocument>(pingCommand, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            sw.Stop();
            var latency = sw.ElapsedMilliseconds;
            var data = new Dictionary<string, object>
            {
                ["latency_ms"] = latency,
                ["database"] = _clientProvider.Database.DatabaseNamespace.DatabaseName
            };

            if (latency > 1000)
            {
                _logger.LogWarning("MongoDB latency high: {Ms}ms", latency);
                return HealthCheckResult.Degraded($"High latency ({latency}ms)", data: data);
            }

            return HealthCheckResult.Healthy($"Healthy (ping: {latency}ms)", data: data);
        }
        catch (MongoAuthenticationException ex)
        {
            _logger.LogError(ex, "MongoDB auth failed");
            return HealthCheckResult.Unhealthy("Authentication failed", ex);
        }
        catch (MongoConnectionException ex)
        {
            _logger.LogError(ex, "MongoDB connection failed");
            return HealthCheckResult.Unhealthy("Connection failed", ex);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "MongoDB timeout");
            return HealthCheckResult.Unhealthy("Timeout", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB health check failed");
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
