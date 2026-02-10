using Company.Persistence.Abstractions.Repositories;
using Company.Persistence.Abstractions.Results;

namespace Company.Persistence.Example.Domain;

public interface ICustomerRepository : IRepository<Customer, CustomerId>
{
    Task<Result<Customer>> GetByEmailAsync(string email, CancellationToken ct);
}
