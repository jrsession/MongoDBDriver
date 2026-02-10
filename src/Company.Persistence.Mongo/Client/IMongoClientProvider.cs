// =============================================================================
// FILE: Client/IMongoClientProvider.cs
// PURPOSE: Abstraction for MongoClient access
// =============================================================================

using MongoDB.Driver;

namespace Company.Persistence.Mongo.Client;

/// <summary>
/// Provides access to the configured MongoDB client singleton.
/// </summary>
/// <remarks>
/// <para><b>Design Decision:</b> Interface for testability.
/// Production uses real MongoClient; tests can mock or use Testcontainers.</para>
/// <para><b>Lifecycle:</b> The underlying <see cref="IMongoClient"/> is a singleton.
/// Creating new clients is expensive and should be avoided.</para>
/// </remarks>
/// <example>
/// <code>
/// // In repository implementation
/// public class CustomerRepository(IMongoClientProvider clientProvider)
/// {
///     private readonly IMongoCollection&lt;CustomerDocument&gt; _collection =
///         clientProvider.GetCollection&lt;CustomerDocument&gt;("customers");
/// }
///
/// // In tests with mocking
/// var mockProvider = new Mock&lt;IMongoClientProvider&gt;();
/// mockProvider.Setup(p => p.GetCollection&lt;CustomerDocument&gt;("customers"))
///     .Returns(mockCollection.Object);
/// </code>
/// </example>
public interface IMongoClientProvider
{
    /// <summary>
    /// Gets the configured MongoDB client (singleton).
    /// </summary>
    /// <remarks>
    /// The client manages its own connection pool internally.
    /// Do not dispose this client - it's managed by the DI container.
    /// </remarks>
    IMongoClient Client { get; }

    /// <summary>
    /// Gets the configured database instance.
    /// </summary>
    /// <remarks>
    /// The database name is determined by <see cref="Configuration.MongoSettings.DatabaseName"/>.
    /// </remarks>
    IMongoDatabase Database { get; }

    /// <summary>
    /// Gets a typed collection with the specified name.
    /// </summary>
    /// <typeparam name="TDocument">The document type.</typeparam>
    /// <param name="collectionName">The collection name.</param>
    /// <returns>A typed collection handle.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="collectionName"/> is null or whitespace.
    /// </exception>
    IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName);
}
