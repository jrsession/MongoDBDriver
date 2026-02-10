// =============================================================================
// FILE: Mapping/IEntityMapper.cs
// PURPOSE: Bidirectional mapping between domain entities and MongoDB documents
// =============================================================================

using Company.Persistence.Mongo.Documents;

namespace Company.Persistence.Mongo.Mapping;

/// <summary>
/// Maps between domain entities and MongoDB documents.
/// </summary>
/// <typeparam name="TEntity">The domain entity type.</typeparam>
/// <typeparam name="TId">The strongly-typed ID type.</typeparam>
/// <typeparam name="TDocument">The MongoDB document type.</typeparam>
/// <remarks>
/// <para><b>Design Decision:</b> Explicit mapping over AutoMapper because:</para>
/// <list type="bullet">
///   <item><b>No hidden magic:</b> All mappings are visible in code</item>
///   <item><b>Compile-time safety:</b> Property mismatches caught at build time</item>
///   <item><b>Clear separation:</b> Domain and persistence schemas evolve independently</item>
///   <item><b>Testability:</b> Easy to unit test mapping logic</item>
/// </list>
/// <para><b>Responsibilities:</b></para>
/// <list type="bullet">
///   <item>Convert domain entity to MongoDB document</item>
///   <item>Convert MongoDB document back to domain entity</item>
///   <item>Parse/format strongly-typed IDs to/from strings</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// public sealed class CustomerMapper : IEntityMapper&lt;Customer, CustomerId, CustomerDocument&gt;
/// {
///     public CustomerDocument ToDocument(Customer entity) => new()
///     {
///         Id = FormatId(entity.Id),
///         Name = entity.Name,
///         Email = entity.Email
///     };
///
///     public Customer ToDomain(CustomerDocument document)
///     {
///         // Create entity and map properties
///     }
///
///     public CustomerId ParseId(string id) => CustomerId.From(id);
///     public string FormatId(CustomerId id) => id.ToString();
/// }
/// </code>
/// </example>
public interface IEntityMapper<TEntity, TId, TDocument>
    where TEntity : class
    where TId : notnull
    where TDocument : MongoDocumentBase
{
    /// <summary>
    /// Converts a domain entity to a MongoDB document.
    /// </summary>
    /// <param name="entity">The domain entity to convert.</param>
    /// <returns>The MongoDB document representation.</returns>
    /// <remarks>
    /// <para>Audit fields (CreatedAt, CreatedBy, Version, etc.) are handled separately
    /// by the repository and should not be set by this method.</para>
    /// <para>The document's <c>Id</c> field should be set using <see cref="FormatId"/>.</para>
    /// </remarks>
    TDocument ToDocument(TEntity entity);

    /// <summary>
    /// Converts a MongoDB document to a domain entity.
    /// </summary>
    /// <param name="document">The MongoDB document to convert.</param>
    /// <returns>The domain entity.</returns>
    /// <remarks>
    /// <para>All fields including audit fields should be mapped back to the entity.</para>
    /// <para>The entity's Id should be set using <see cref="ParseId"/>.</para>
    /// </remarks>
    TEntity ToDomain(TDocument document);

    /// <summary>
    /// Parses a string ID to the strongly-typed ID.
    /// </summary>
    /// <param name="id">The string representation of the ID.</param>
    /// <returns>The strongly-typed ID.</returns>
    /// <exception cref="FormatException">
    /// Thrown if the string cannot be parsed to the ID type.
    /// </exception>
    TId ParseId(string id);

    /// <summary>
    /// Formats a strongly-typed ID as a string for storage.
    /// </summary>
    /// <param name="id">The strongly-typed ID.</param>
    /// <returns>The string representation for storage.</returns>
    string FormatId(TId id);
}
