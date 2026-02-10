// =============================================================================
// FILE: Indexes/IIndexDefinitionProvider.cs
// PURPOSE: Contract for defining collection indexes
// =============================================================================

using MongoDB.Driver;

namespace Company.Persistence.Mongo.Indexes;

/// <summary>
/// Provides index definitions for a MongoDB collection.
/// </summary>
/// <typeparam name="TDocument">The document type for the collection.</typeparam>
/// <remarks>
/// <para><b>Design Decision:</b> Interface enables per-entity index configuration
/// while keeping index creation centralized in <see cref="IndexInitializer"/>.</para>
/// <para><b>Usage:</b> Implement this interface for each collection that needs custom indexes.
/// Indexes are created idempotently at application startup.</para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class CustomerIndexProvider : IIndexDefinitionProvider&lt;CustomerDocument&gt;
/// {
///     public IEnumerable&lt;CreateIndexModel&lt;CustomerDocument&gt;&gt; GetIndexes()
///     {
///         // Unique index on email
///         yield return new CreateIndexModel&lt;CustomerDocument&gt;(
///             Builders&lt;CustomerDocument&gt;.IndexKeys.Ascending(d => d.Email),
///             new CreateIndexOptions { Name = "ix_customers_email", Unique = true });
///
///         // Compound index for regional queries
///         yield return new CreateIndexModel&lt;CustomerDocument&gt;(
///             Builders&lt;CustomerDocument&gt;.IndexKeys
///                 .Ascending(d => d.Region)
///                 .Ascending(d => d.IsDeleted),
///             new CreateIndexOptions { Name = "ix_customers_region" });
///     }
/// }
/// </code>
/// </example>
public interface IIndexDefinitionProvider<TDocument>
{
    /// <summary>
    /// Gets the index definitions for this collection.
    /// </summary>
    /// <returns>
    /// A collection of index models to create. Indexes are created idempotently
    /// (existing indexes with the same name are skipped).
    /// </returns>
    IEnumerable<CreateIndexModel<TDocument>> GetIndexes();
}
