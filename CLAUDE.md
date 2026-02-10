# Company.Persistence - MongoDB Driver Library

## Overview
Enterprise-grade MongoDB persistence library for .NET 8, designed as a shared NuGet package across multiple services. Abstracts MongoDB implementation details behind clean interfaces with built-in audit, soft delete, and observability.

## Tech Stack
- **Runtime:** .NET 8
- **Language:** C# 12+ (latest features)
- **Database:** MongoDB Atlas (mongodb+srv://)
- **Driver:** MongoDB.Driver 3.1.0 (official NuGet package)
- **Observability:** OpenTelemetry + Application Insights

## Project Structure
```
src/
├── Company.Persistence.Abstractions/    # Contracts only, zero dependencies
│   ├── Entities/                        # IEntity, IAuditableEntity, ISoftDeletable
│   ├── Repositories/                    # IRepository, IReadOnlyRepository
│   ├── Results/                         # Result<T>, Error, ErrorCodes
│   ├── Paging/                          # PagedRequest, PagedResult
│   ├── Audit/                           # IUserContextProvider, AuditEntry
│   └── UnitOfWork/                      # IUnitOfWork, ITransaction
│
├── Company.Persistence.Mongo/           # MongoDB implementation
│   ├── Configuration/                   # MongoSettings (Atlas-optimized)
│   ├── Client/                          # MongoClientProvider (singleton)
│   ├── Documents/                       # MongoDocumentBase (internal)
│   ├── Mapping/                         # IEntityMapper, EntityMapperBase
│   ├── Repositories/                    # MongoRepositoryBase
│   ├── Indexes/                         # IIndexDefinitionProvider
│   ├── Health/                          # MongoHealthCheck
│   ├── Observability/                   # OpenTelemetry instrumentation
│   └── Extensions/                      # DI registration
│
tests/
├── Company.Persistence.Abstractions.Tests/
└── Company.Persistence.Mongo.Tests/
```

## Architecture Principles
1. **No MongoDB leakage** - ObjectId, BsonDocument, FilterDefinition never exposed
2. **Result<T> monad** - Explicit error handling, no exceptions for business errors
3. **Audit by default** - CreatedAt/By, UpdatedAt/By, Version on all entities
4. **Soft delete** - ISoftDeletable entities are never physically deleted
5. **Optimistic concurrency** - Version field prevents lost updates
6. **Pagination required** - No unbounded GetAll() queries

## Coding Standards (Modern C# 12+)

### Required Patterns
```csharp
// Use required + init for mandatory immutable properties
public sealed class MongoSettings
{
    public required string ConnectionString { get; init; }
}

// Use primary constructors for DI
public sealed class CustomerRepository(
    IMongoClientProvider client,
    ILogger<CustomerRepository> logger) : ICustomerRepository
{
}

// Use records for immutable data types
public sealed record Error(string Code, string Message);

// Use readonly record struct for value objects
public readonly record struct CustomerId(Guid Value);

// Use pattern matching
return result switch
{
    { IsSuccess: true } => Ok(result.Value),
    { Error.Code: ErrorCodes.NotFound } => NotFound(),
    _ => Problem(result.Error!.Message)
};
```

### Forbidden Patterns
- `public set` properties (use `init` or `private set`)
- `Task.Result` or `.Wait()` (deadlock risk)
- `async void` (unhandled exceptions)
- Service Locator pattern
- Static helper classes (use DI)
- Constructor overloads (use `required` instead)

### Async Rules
- CancellationToken is **required** (not optional) on all async methods
- Use `ConfigureAwait(false)` in library code
- Use `ValueTask` for hot paths that often complete synchronously

## Key Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| MongoDB.Driver | 3.1.0 | Official MongoDB C# driver |
| Microsoft.Extensions.Options | 8.0.x | Strongly-typed configuration |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.x | DI contracts |
| Microsoft.Extensions.Diagnostics.HealthChecks | 8.0.x | Health checks |

## Atlas Configuration Defaults
| Setting | Default | Rationale |
|---------|---------|-----------|
| ReadPreference | secondaryPreferred | Cost optimization - reads from replicas |
| WriteConcern | majority | Durability - survives failover |
| ConnectTimeout | 10s | Atlas cold start latency |
| ServerSelectionTimeout | 30s | Atlas failover window |
| RetryWrites | true | Transient failure handling |

## Testing Strategy
- **Unit tests:** Result monad, mappers, paging logic (xUnit)
- **Integration tests:** Full CRUD with Testcontainers (MongoDB)
- **Test categories:** `[Trait("Category", "Unit")]`, `[Trait("Category", "Integration")]`

## Documentation Requirements
All public APIs must include:
- XML `<summary>` documentation
- `<param>` and `<returns>` descriptions
- `<example>` with copy-pasteable code
- `<remarks>` explaining when/why to use

## Commands
```bash
# Build
dotnet build --configuration Release

# Test
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"  # Requires Docker

# Pack for NuGet
dotnet pack --configuration Release
```
