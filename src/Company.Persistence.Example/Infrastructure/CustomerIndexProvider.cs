using Company.Persistence.Mongo.Indexes;
using MongoDB.Driver;

namespace Company.Persistence.Example.Infrastructure;

/// <summary>
/// Defines indexes for the customers collection.
/// </summary>
public sealed class CustomerIndexProvider : IIndexDefinitionProvider<CustomerDocument>
{
    public IEnumerable<CreateIndexModel<CustomerDocument>> GetIndexes()
    {
        // Unique index on email
        yield return new CreateIndexModel<CustomerDocument>(
            Builders<CustomerDocument>.IndexKeys.Ascending(d => d.Email),
            new CreateIndexOptions
            {
                Name = "ix_customers_email",
                Unique = true
            });

        // Index for soft delete filtering
        yield return new CreateIndexModel<CustomerDocument>(
            Builders<CustomerDocument>.IndexKeys.Ascending(d => d.IsDeleted),
            new CreateIndexOptions { Name = "ix_customers_isdeleted" });
    }
}
