// =============================================================================
// FILE: Extensions/ServiceCollectionExtensions.cs
// PURPOSE: DI registration extensions for MongoDB persistence
// =============================================================================

using Company.Persistence.Abstractions.Audit;
using Company.Persistence.Abstractions.Entities;
using Company.Persistence.Abstractions.Repositories;
using Company.Persistence.Abstractions.UnitOfWork;
using Company.Persistence.Mongo.Audit;
using Company.Persistence.Mongo.Client;
using Company.Persistence.Mongo.Configuration;
using Company.Persistence.Mongo.Documents;
using Company.Persistence.Mongo.Health;
using Company.Persistence.Mongo.Indexes;
using Company.Persistence.Mongo.Mapping;
using Company.Persistence.Mongo.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Company.Persistence.Mongo.Extensions;

/// <summary>
/// Extension methods for configuring MongoDB persistence in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MongoDB persistence services with inline configuration.
    /// </summary>
    public static IServiceCollection AddMongoPersistence(
        this IServiceCollection services,
        Action<MongoSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<MongoSettings>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterCoreServices(services);
        return services;
    }

    /// <summary>
    /// Adds MongoDB persistence services from configuration section.
    /// </summary>
    public static IServiceCollection AddMongoPersistence(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string configurationSection = "MongoDB")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MongoSettings>()
            .BindConfiguration(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterCoreServices(services);
        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddSingleton<IMongoClientProvider, MongoClientProvider>();
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IAuditHistoryWriter, AuditHistoryWriter>();
        services.AddHostedService<IndexInitializer>();

        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>(
                name: "mongodb",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "live", "mongodb"]);
    }

    /// <summary>
    /// Registers a MongoDB repository with its interface.
    /// </summary>
    public static IServiceCollection AddMongoRepository<TEntity, TId, TDocument, TRepository, TInterface>(
        this IServiceCollection services,
        string collectionName)
        where TEntity : class, IEntity<TId>
        where TId : notnull
        where TDocument : MongoDocumentBase
        where TRepository : class, TInterface
        where TInterface : class, IRepository<TEntity, TId>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        services.AddSingleton(new CollectionNameRegistration<TDocument>(collectionName));
        services.AddScoped<TInterface, TRepository>();

        return services;
    }

    /// <summary>
    /// Registers a MongoDB repository with its mapper.
    /// </summary>
    public static IServiceCollection AddMongoRepository<TEntity, TId, TDocument, TRepository, TInterface, TMapper>(
        this IServiceCollection services,
        string collectionName)
        where TEntity : class, IEntity<TId>
        where TId : notnull
        where TDocument : MongoDocumentBase
        where TRepository : class, TInterface
        where TInterface : class, IRepository<TEntity, TId>
        where TMapper : class, IEntityMapper<TEntity, TId, TDocument>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        services.AddSingleton(new CollectionNameRegistration<TDocument>(collectionName));
        services.TryAddSingleton<IEntityMapper<TEntity, TId, TDocument>, TMapper>();
        services.AddScoped<TInterface, TRepository>();

        return services;
    }

    /// <summary>
    /// Registers an index provider for a collection.
    /// </summary>
    public static IServiceCollection AddIndexProvider<TDocument, TProvider>(
        this IServiceCollection services,
        string collectionName)
        where TDocument : class
        where TProvider : class, IIndexDefinitionProvider<TDocument>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        services.AddSingleton<IIndexDefinitionProvider<TDocument>, TProvider>();
        services.AddSingleton<IndexRegistration>(sp =>
            new IndexRegistration<TDocument>(
                sp.GetRequiredService<IMongoClientProvider>(),
                sp.GetRequiredService<IIndexDefinitionProvider<TDocument>>(),
                collectionName,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IndexRegistration<TDocument>>>()));

        return services;
    }

    /// <summary>
    /// Registers a default IUserContextProvider that returns "system".
    /// </summary>
    public static IServiceCollection AddDefaultUserContextProvider(this IServiceCollection services)
    {
        services.TryAddScoped<IUserContextProvider, DefaultUserContextProvider>();
        return services;
    }
}

internal sealed class CollectionNameRegistration<TDocument>(string collectionName)
{
    public string CollectionName { get; } = collectionName;
}

internal sealed class DefaultUserContextProvider : IUserContextProvider
{
    public string GetCurrentUserId() => "system";
    public string? GetCorrelationId() => System.Diagnostics.Activity.Current?.TraceId.ToString();
}
