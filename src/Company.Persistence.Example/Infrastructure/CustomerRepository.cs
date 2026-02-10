using Company.Persistence.Abstractions.Audit;
using Company.Persistence.Abstractions.Results;
using Company.Persistence.Example.Domain;
using Company.Persistence.Mongo.Audit;
using Company.Persistence.Mongo.Client;
using Company.Persistence.Mongo.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Company.Persistence.Example.Infrastructure;

/// <summary>
/// MongoDB repository implementation for Customer entities.
/// </summary>
public sealed class CustomerRepository : MongoRepositoryBase<Customer, CustomerId, CustomerDocument>, ICustomerRepository
{
    public CustomerRepository(
        IMongoClientProvider clientProvider,
        CustomerMapper mapper,
        IUserContextProvider userContext,
        IAuditHistoryWriter auditWriter,
        ILogger<CustomerRepository> logger)
        : base(clientProvider, mapper, userContext, auditWriter, logger, "customers")
    {
    }

    public async Task<Result<Customer>> GetByEmailAsync(string email, CancellationToken ct)
    {
        var filter = Builders<CustomerDocument>.Filter.Eq(d => d.Email, email);
        return await GetSingleAsync(filter, ct).ConfigureAwait(false);
    }
}
