# Company.Persistence - Architecture & Design Decisions

> **Version:** 1.0.0
> **Last Updated:** 2026-02-06
> **Target Framework:** .NET 8
> **MongoDB Driver:** 3.1.0 (Official NuGet Package)

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Design Principles](#design-principles)
3. [Package Architecture](#package-architecture)
4. [Key Architectural Decisions (ADRs)](#key-architectural-decisions-adrs)
5. [MongoDB Driver Configuration](#mongodb-driver-configuration)
6. [Atlas Optimization Strategy](#atlas-optimization-strategy)
7. [Audit & Change Tracking](#audit--change-tracking)
8. [Observability Strategy](#observability-strategy)
9. [Error Handling Philosophy](#error-handling-philosophy)
10. [Security Considerations](#security-considerations)
11. [Extensibility Points](#extensibility-points)
12. [Performance Considerations](#performance-considerations)
13. [Quick Reference](#quick-reference)

---

## Executive Summary

### What This Library Is

A **governed, enterprise-grade persistence layer** for MongoDB Atlas that:

- Abstracts MongoDB implementation details behind clean interfaces
- Enforces organizational standards (audit, soft delete, pagination)
- Provides Atlas-optimized defaults for cost and performance
- Integrates seamlessly with Application Insights and OpenTelemetry

### What This Library Is NOT

| Anti-Goal | Rationale |
|-----------|-----------|
| **Not an ORM** | No change tracking, lazy loading, or navigation properties. Explicit operations only. |
| **Not a query builder** | Use MongoDB's `FilterDefinition<T>` for custom queries. No LINQ provider abstraction. |
| **Not framework-agnostic** | Designed specifically for .NET 8 and MongoDB Atlas. Optimized for this stack. |
| **Not a generic repository** | Opinionated patterns enforced: pagination required, soft delete by default, audit always. |

### Goals

| Goal | How We Achieve It |
|------|-------------------|
| **Abstraction without leakage** | No `ObjectId`, `BsonDocument`, `FilterDefinition` in public API |
| **Audit by default** | Every mutation tracked: who, when, what changed |
| **Safe defaults** | Pagination required, soft delete enforced, bounded queries |
| **Observable** | OpenTelemetry traces, structured logging, health checks |
| **Cost-optimized** | Atlas-specific defaults to minimize spend (secondary reads) |
| **Testable** | Interfaces for all components, Testcontainers for integration |

---

## Design Principles

### 1. Explicit Over Implicit

Every operation requires explicit parameters. No hidden magic:

```csharp
// CancellationToken is REQUIRED (not optional)
Task<Result<Customer>> GetByIdAsync(CustomerId id, CancellationToken cancellationToken);

// Pagination is REQUIRED for list operations
Task<Result<PagedResult<Customer>>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken);

// Transactions are OPT-IN via IUnitOfWork
await using var tx = await unitOfWork.BeginTransactionAsync(ct);
```

### 2. Fail Fast, Fail Loud

- Connection failures throw immediately (no silent retries beyond driver defaults)
- Concurrency conflicts return explicit `Error.Conflict`
- Missing entities return `Error.NotFound` (not null)
- Invalid configuration fails at startup with clear messages

### 3. Composition Over Inheritance

```csharp
// Preferred: Composition via constructor injection
public sealed class CustomerRepository(
    IMongoClientProvider clientProvider,
    IEntityMapper<Customer, CustomerId, CustomerDocument> mapper,
    IUserContextProvider userContext,
    ILogger<CustomerRepository> logger) : ICustomerRepository
{
    // Delegate to base implementation
}

// Acceptable: Single level of inheritance for shared behavior
public abstract class MongoRepositoryBase<TEntity, TId, TDocument> : IRepository<TEntity, TId>
{
    // Shared CRUD implementation
}
```

### 4. Domain-Driven Boundaries

```
┌─────────────────────────────────────────────────────────────────┐
│                     Application Layer                           │
│  (Controllers, Services, Commands, Queries)                     │
│  • Depends on: Abstractions                                     │
│  • Uses: IRepository<T>, Result<T>, PagedResult<T>             │
├─────────────────────────────────────────────────────────────────┤
│                      Domain Layer                               │
│  (Entities, Value Objects, Domain Services)                     │
│  • Depends on: Abstractions                                     │
│  • Implements: IAuditableEntity<TId>, ISoftDeletable           │
├─────────────────────────────────────────────────────────────────┤
│            Company.Persistence.Abstractions                     │
│  (Interfaces, Records, Contracts)                               │
│  • Dependencies: NONE (except .NET BCL)                         │
│  • Exposes: IRepository, Result<T>, Error, PagedRequest        │
├─────────────────────────────────────────────────────────────────┤
│              Company.Persistence.Mongo                          │
│  (MongoDB Implementation)                                       │
│  • Dependencies: Abstractions + MongoDB.Driver 3.1.0           │
│  • INTERNAL: ObjectId, BsonDocument, FilterDefinition          │
├─────────────────────────────────────────────────────────────────┤
│                    MongoDB Atlas                                │
│  (Cloud Database Service)                                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Package Architecture

### Company.Persistence.Abstractions

**Purpose:** Define contracts that domain and application layers depend on.

**Dependencies:** NONE (except .NET BCL)

**Why a separate package?**

| Reason | Benefit |
|--------|---------|
| Domain layer purity | Domain code has no MongoDB driver dependency |
| Testing | Create in-memory fakes without MongoDB |
| Future flexibility | Could implement CosmosDB, PostgreSQL adapters |
| NuGet versioning | Abstractions can be versioned independently |

**Namespace Structure:**

```
Company.Persistence.Abstractions
├── Entities/
│   ├── IEntity<TId>              # Base identity contract
│   ├── IAuditableEntity<TId>     # Audit + version fields
│   └── ISoftDeletable            # Soft delete marker
├── Repositories/
│   ├── IReadOnlyRepository<T,TId># Query operations
│   └── IRepository<T,TId>        # Full CRUD
├── Results/
│   ├── Result<T>                 # Success/Failure monad
│   ├── Error                     # Structured error
│   ├── ErrorCodes                # Well-known codes
│   └── Unit                      # Void equivalent
├── Paging/
│   ├── PagedRequest              # Pagination input
│   └── PagedResult<T>            # Pagination output
├── Audit/
│   ├── IUserContextProvider      # Current user resolver
│   ├── AuditAction               # Created/Updated/Deleted
│   └── AuditEntry                # Change history record
└── UnitOfWork/
    ├── IUnitOfWork               # Transaction factory
    └── ITransaction              # Transaction scope
```

### Company.Persistence.Mongo

**Purpose:** MongoDB Atlas implementation of all abstractions.

**Dependencies:**
- `Company.Persistence.Abstractions`
- `MongoDB.Driver` 3.1.0 (official driver from https://www.nuget.org/packages/MongoDB.Driver)
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Diagnostics.HealthChecks`
- `Microsoft.Extensions.Logging.Abstractions`

**Internal Types (never exposed in public API):**

| MongoDB Type | Why Internal |
|--------------|--------------|
| `ObjectId` | Implementation detail of document identity |
| `BsonDocument` | Raw document representation |
| `FilterDefinition<T>` | Query building is internal |
| `UpdateDefinition<T>` | Update operations are internal |
| `IMongoCollection<T>` | Collection access is internal |
| `IClientSessionHandle` | Transaction sessions are internal |

**Namespace Structure:**

```
Company.Persistence.Mongo
├── Configuration/
│   ├── MongoSettings             # Strongly-typed config
│   └── CollectionSettings        # Per-collection options
├── Client/
│   ├── IMongoClientProvider      # Client abstraction
│   └── MongoClientProvider       # Singleton implementation
├── Documents/
│   ├── MongoDocumentBase         # Base document (internal)
│   └── AuditHistoryDocument      # Audit storage (internal)
├── Mapping/
│   ├── IEntityMapper<T,TId,TDoc> # Mapping contract
│   └── EntityMapperBase          # Base with audit handling
├── Repositories/
│   ├── MongoRepositoryBase       # Full CRUD implementation
│   └── MongoRepositoryContext    # Scoped dependencies
├── Indexes/
│   ├── IIndexDefinitionProvider  # Index definitions
│   └── IndexInitializer          # Startup index creation
├── Health/
│   └── MongoHealthCheck          # Ping-based health
├── Observability/
│   ├── MongoTelemetry            # Activity source
│   └── DiagnosticsSubscriber     # Command events
├── UnitOfWork/
│   └── MongoUnitOfWork           # Transaction implementation
└── Extensions/
    └── ServiceCollectionExtensions # DI registration
```

---

## Key Architectural Decisions (ADRs)

### ADR-001: Result<T> Monad vs Exceptions

**Status:** Accepted

**Context:**
Repository operations have predictable failure modes (not found, conflict, validation). These are not exceptional—they're expected outcomes that callers must handle.

**Decision:** Use `Result<T>` for all repository operations.

**Comparison:**

| Approach | Pros | Cons |
|----------|------|------|
| **Exceptions** | Familiar to .NET devs, stack traces | Hidden control flow, performance cost, easy to forget |
| **Result<T>** | Explicit, composable, no hidden jumps | Learning curve, more verbose |
| **Nullable** | Simple | Loses error context, ambiguous |

**Consequences:**
- All repository methods return `Result<T>` or `Result<Unit>`
- Callers must pattern match: `result.Match(success: ..., failure: ...)`
- Infrastructure failures (network, timeout) still throw exceptions
- Code is more verbose but explicitly handles all outcomes

**Example:**

```csharp
// Correct: Explicit handling
var result = await repository.GetByIdAsync(id, ct);
return result switch
{
    { IsSuccess: true, Value: var customer } => Ok(customer),
    { Error.Code: ErrorCodes.NotFound } => NotFound(),
    { Error: var error } => Problem(error.Message)
};

// WRONG: Defeats the purpose
var entity = (await repository.GetByIdAsync(id, ct)).Value!; // Throws if failure
```

---

### ADR-002: Strongly-Typed IDs

**Status:** Accepted

**Context:**
Using primitive types (`Guid`, `string`) for entity IDs leads to bugs where IDs from different entities are accidentally interchanged.

**Decision:** Support strongly-typed IDs using `readonly record struct`.

**Problem Example:**

```csharp
// Bug: Accidentally passed OrderId where CustomerId expected
await customerRepo.GetByIdAsync(order.Id, ct); // Compiles but WRONG!
```

**Solution:**

```csharp
// Strongly-typed ID
public readonly record struct CustomerId(Guid Value) : IEquatable<CustomerId>
{
    public static CustomerId New() => new(Guid.NewGuid());
    public static CustomerId From(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

// Now the compiler catches the bug
await customerRepo.GetByIdAsync(order.Id, ct); // Compile ERROR!
```

**Storage Format:**
IDs are stored as strings in MongoDB for flexibility and human readability:

```json
{
  "_id": ObjectId("..."),
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Acme Corp"
}
```

---

### ADR-003: Separate Audit History Collection

**Status:** Accepted

**Context:**
We need to track all changes (create, update, delete) for compliance and debugging.

**Alternatives Considered:**

| Approach | Pros | Cons |
|----------|------|------|
| Embedded array | Single read for entity + history | Document bloat, 16MB limit |
| Same collection (versions) | Simple queries | Pollutes main collection |
| **Separate collection** | Clean separation, unlimited history | Extra query for history |

**Decision:** Store change history in a dedicated `_audit_history` collection.

**Rationale:**
- Audit history can grow unbounded—separate collection avoids 16MB document limit
- Can apply TTL index for retention policy (e.g., 90 days)
- Main collection stays lean for operational queries
- Audit queries are typically admin-only, low frequency

**Schema:**

```json
// Collection: _audit_history
{
  "_id": ObjectId("..."),
  "entityType": "Customer",
  "entityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "action": "Updated",
  "userId": "user@company.com",
  "timestamp": ISODate("2026-02-06T10:30:00Z"),
  "correlationId": "abc-123-def",
  "oldValue": { /* previous state */ },
  "newValue": { /* new state */ }
}
```

---

### ADR-004: Optimistic Concurrency via Version Field

**Status:** Accepted

**Context:**
Concurrent updates can cause lost writes without proper concurrency control.

**Problem Scenario:**
1. User A reads entity (version 1)
2. User B reads entity (version 1)
3. User A updates → version 2
4. User B updates → **overwrites User A's changes** (lost update)

**Decision:** Every auditable entity has a `Version` field (incrementing `long`).

**Implementation:**

```csharp
// Update includes version check
var filter = Builders<T>.Filter.And(
    Builders<T>.Filter.Eq(d => d.Id, id),
    Builders<T>.Filter.Eq(d => d.Version, expectedVersion)  // Optimistic lock
);

var update = Builders<T>.Update
    .Set(d => d.Name, newName)
    .Inc(d => d.Version, 1);  // Increment version

var result = await collection.UpdateOneAsync(filter, update, ct);

if (result.ModifiedCount == 0)
{
    // Version mismatch - concurrent modification detected
    return Error.ConcurrencyConflict(entityType, id);
}
```

**Consequences:**
- Updates may fail with `Error.Conflict`—callers must handle (retry or inform user)
- No need for database-level pessimistic locking
- Works correctly across distributed services
- Version is automatically incremented by repository

---

### ADR-005: Soft Delete by Default

**Status:** Accepted

**Context:**
Physical deletion of data has risks: audit trail loss, accidental deletion, compliance issues.

**Decision:** Entities implementing `ISoftDeletable` are never physically deleted.

**Rationale:**

| Benefit | Description |
|---------|-------------|
| **Audit trail** | Know who deleted what and when |
| **Recovery** | Accidental deletions are reversible |
| **Compliance** | Many regulations require data retention |
| **Referential integrity** | Foreign references don't break |

**Implementation:**

```csharp
// Delete operation sets flags instead of removing document
public async Task<Result<Unit>> DeleteAsync(TId id, CancellationToken ct)
{
    var update = Builders<T>.Update
        .Set(d => d.IsDeleted, true)
        .Set(d => d.DeletedAt, DateTimeOffset.UtcNow)
        .Set(d => d.DeletedBy, _userContext.GetCurrentUserId());

    await _collection.UpdateOneAsync(filter, update, ct);
    await _auditWriter.WriteAsync(entity, AuditAction.Deleted, ct);

    return Unit.Value;
}
```

**All queries automatically filter soft-deleted entities:**

```csharp
// Applied to every query in MongoRepositoryBase
private static readonly FilterDefinition<TDocument> NotDeletedFilter =
    Builders<TDocument>.Filter.Eq(d => d.IsDeleted, false);
```

---

### ADR-006: Pagination Required for All List Operations

**Status:** Accepted

**Context:**
Unbounded queries (`GetAll()`) are dangerous—they can exhaust memory, timeout, and cause cascading failures.

**Decision:** All list operations require `PagedRequest` with enforced maximum page size.

**Implementation:**

```csharp
public readonly record struct PagedRequest
{
    public const int MaxPageSize = 100;  // Hard limit
    public const int DefaultPageSize = 20;

    public int Page { get; }      // 1-based, minimum 1
    public int PageSize { get; }  // Clamped to MaxPageSize
}
```

**Consequences:**
- No `GetAll()` method exists
- Callers must think about pagination from the start
- Large datasets are naturally handled in chunks
- API responses have predictable size

---

## MongoDB Driver Configuration

### Why MongoDB.Driver 3.1.0?

The official MongoDB C# driver from https://www.nuget.org/packages/MongoDB.Driver is:

| Feature | Benefit |
|---------|---------|
| **Official** | Maintained by MongoDB Inc., guaranteed compatibility |
| **Async-native** | Full `async/await` support, no blocking calls |
| **Modern** | Supports all MongoDB 7.x features |
| **Observable** | Built-in OpenTelemetry instrumentation |
| **Battle-tested** | 345M+ downloads, used in production globally |

### MongoClient Lifecycle

**Critical:** `MongoClient` is a **singleton**. Creating new clients is expensive:

```csharp
// CORRECT: Single instance, reused across all requests
services.AddSingleton<IMongoClientProvider, MongoClientProvider>();

// WRONG: Creates new client per request (connection pool thrashing)
services.AddScoped<IMongoClient>(_ => new MongoClient(connectionString));
```

### Connection Pooling

The driver manages an internal connection pool:

| Setting | Default | Meaning |
|---------|---------|---------|
| `MaxConnectionPoolSize` | 100 | Maximum connections per server |
| `MinConnectionPoolSize` | 5 | Kept warm to avoid cold starts |
| `WaitQueueTimeout` | 2 min | Time to wait for available connection |

---

## Atlas Optimization Strategy

### Read Preference: `secondaryPreferred`

**What it does:** Routes reads to replica set secondaries when available.

**Cost/Performance Impact:**

| Setting | Cost Impact | Consistency | Use Case |
|---------|-------------|-------------|----------|
| `primary` | $$$ Higher | Strong | Financial transactions |
| `primaryPreferred` | $$ Medium | Strong (usually) | Most apps |
| **`secondaryPreferred`** | **$ Lower** | Eventual (ms lag) | Read-heavy workloads |
| `secondary` | $ Lower | Eventual | Analytics only |

**Why this default:**
- Atlas pricing is based on primary node utilization
- Reading from secondaries distributes load across the replica set
- Reduces primary CPU/memory pressure by 40-60%
- Can enable using a smaller (cheaper) Atlas tier

**When to override:**

```csharp
// For operations requiring strong consistency
options.ReadPreference = "primary";
```

### Write Concern: `majority`

**What it does:** Write is acknowledged only after replicated to majority of nodes.

**Why this default:**
- Required for multi-document transactions
- Survives primary failover without data loss
- Atlas best practice for production workloads

**Trade-off:** ~10-20ms additional latency vs `w:1`, but guaranteed durability.

### Timeouts Explained

| Setting | Default | Why This Value |
|---------|---------|----------------|
| `ConnectTimeout` | 10s | Atlas serverless/shared tiers have cold start latency (5-7s) |
| `ServerSelectionTimeout` | 30s | Atlas automatic failover takes 10-30s to elect new primary |
| `SocketTimeout` | 60s | Long-running aggregations need time to complete |
| `MaxConnectionPoolSize` | 100 | Sufficient for most apps; Atlas M10+ supports 1500 |

---

## Audit & Change Tracking

### Automatic Audit Fields

Every mutation automatically populates:

| Field | On Create | On Update | On Delete |
|-------|-----------|-----------|-----------|
| `CreatedAt` | Set to UTC now | — | — |
| `CreatedBy` | Set to current user | — | — |
| `UpdatedAt` | — | Set to UTC now | — |
| `UpdatedBy` | — | Set to current user | — |
| `DeletedAt` | — | — | Set to UTC now |
| `DeletedBy` | — | — | Set to current user |
| `Version` | Set to 1 | Increment +1 | — |

### User Context Resolution

`IUserContextProvider` resolves the current user:

```csharp
public interface IUserContextProvider
{
    /// <summary>Gets user ID from JWT claims, HTTP context, etc.</summary>
    string GetCurrentUserId();

    /// <summary>Gets correlation ID for distributed tracing.</summary>
    string? GetCorrelationId();
}
```

**Default implementation** reads from `HttpContext.User` claims:
- `sub` claim (OpenID Connect)
- `oid` claim (Azure AD)
- Falls back to `"system"` for background jobs

### Change History Records

Every mutation writes to `_audit_history`:

| Action | OldValue | NewValue |
|--------|----------|----------|
| Created | `null` | Full entity |
| Updated | Previous state | New state |
| Deleted | Entity before delete | `null` |

---

## Observability Strategy

### OpenTelemetry Integration

All MongoDB commands create Activity spans:

```csharp
Activity.Current?.SetTag("db.system", "mongodb");
Activity.Current?.SetTag("db.name", databaseName);
Activity.Current?.SetTag("db.mongodb.collection", collectionName);
Activity.Current?.SetTag("db.operation", commandName);
Activity.Current?.SetTag("db.statement", commandJson);  // Optional
```

### Application Insights Mapping

| OpenTelemetry Attribute | App Insights Field | Example |
|-------------------------|-------------------|---------|
| `db.system=mongodb` | Dependency type | `MongoDB` |
| `db.name` | Target | `myservice-db` |
| `db.operation` | Name | `find`, `insert` |
| `otel.status_code=ERROR` | Success | `false` |

### Structured Logging

All repository operations log with correlation:

```csharp
_logger.LogInformation(
    "MongoDB {Operation} on {Collection} completed in {ElapsedMs}ms. " +
    "EntityId={EntityId}, CorrelationId={CorrelationId}",
    "Update",
    "customers",
    elapsed.TotalMilliseconds,
    entityId,
    correlationId);
```

### Health Checks

Ping-based readiness check:

```csharp
public async Task<HealthCheckResult> CheckHealthAsync(
    HealthCheckContext context,
    CancellationToken ct)
{
    var sw = Stopwatch.StartNew();
    await _database.RunCommandAsync<BsonDocument>(
        new BsonDocument("ping", 1), cancellationToken: ct);

    return HealthCheckResult.Healthy(
        $"MongoDB ping: {sw.ElapsedMilliseconds}ms");
}
```

---

## Error Handling Philosophy

### Error Code Constants

```csharp
public static class ErrorCodes
{
    public const string NotFound = "NOT_FOUND";        // Entity doesn't exist
    public const string Conflict = "CONFLICT";         // Version mismatch
    public const string Validation = "VALIDATION";     // Invalid input
    public const string Duplicate = "DUPLICATE";       // Unique constraint
    public const string Unauthorized = "UNAUTHORIZED"; // No permission
    public const string InternalError = "INTERNAL_ERROR"; // Unexpected
}
```

### Error vs Exception Policy

| Scenario | Handling | Rationale |
|----------|----------|-----------|
| Entity not found | `Result.Failure(Error.NotFound)` | Expected outcome |
| Concurrency conflict | `Result.Failure(Error.Conflict)` | Expected outcome |
| Validation failure | `Result.Failure(Error.Validation)` | Expected outcome |
| Duplicate key | `Result.Failure(Error.Duplicate)` | Expected outcome |
| Network timeout | **THROWS** `MongoTimeoutException` | Infrastructure failure |
| Connection failure | **THROWS** `MongoConnectionException` | Infrastructure failure |
| Auth failure | **THROWS** `MongoAuthenticationException` | Configuration error |

**Rationale:** Business failures are expected and returned as `Result`. Infrastructure failures are exceptional and should bubble up for retry/circuit breaker handling.

---

## Security Considerations

### Connection String Security

| Practice | Status |
|----------|--------|
| Store in Azure Key Vault / AWS Secrets Manager | **Required** |
| Use environment variables for local dev | Acceptable |
| Commit to source control | **NEVER** |
| Log connection strings | **NEVER** |
| Expose in error messages | **NEVER** |

### TLS Enforcement

```csharp
clientSettings.UseTls = true;
clientSettings.AllowInsecureTls = false;  // Reject invalid certificates
```

### Query Injection Prevention

| Practice | Implementation |
|----------|----------------|
| Use typed builders | `Builders<T>.Filter.Eq(d => d.Name, value)` |
| Never concatenate strings | No raw JSON/string queries |
| Parse IDs with known types | `Guid.Parse()`, not arbitrary strings |
| Validate all input | At API boundary, before repository |

---

## Extensibility Points

### Adding a New Entity

1. **Domain entity** implementing `IAuditableEntity<TId>`, `ISoftDeletable`
2. **Strongly-typed ID** as `readonly record struct`
3. **MongoDB document** (internal) extending `MongoDocumentBase`
4. **Mapper** implementing `IEntityMapper<TEntity, TId, TDocument>`
5. **Repository** extending `MongoRepositoryBase`
6. **Index provider** implementing `IIndexDefinitionProvider<TDocument>`
7. **DI registration** via `services.AddMongoRepository<...>()`

### Custom Queries

Derived repositories can add domain-specific queries:

```csharp
public sealed class CustomerRepository : MongoRepositoryBase<Customer, CustomerId, CustomerDocument>
{
    public async Task<Result<PagedResult<Customer>>> GetByRegionAsync(
        string region,
        PagedRequest request,
        CancellationToken ct)
    {
        var filter = Builders<CustomerDocument>.Filter.Eq(d => d.Region, region);
        return await GetPagedAsync(filter, request, ct);
    }
}
```

### Custom Serializers

For complex domain types, implement custom BSON serializers:

```csharp
public sealed class MoneySerializer : IBsonSerializer<Money>
{
    // Custom serialization logic
}

// Register in startup
BsonSerializer.RegisterSerializer(new MoneySerializer());
```

---

## Performance Considerations

### Connection Pool Sizing

| Scenario | Recommended Pool Size |
|----------|----------------------|
| Low traffic (< 100 req/s) | 50 |
| Medium traffic (100-500 req/s) | 100 (default) |
| High traffic (500+ req/s) | 200-500 |
| Very high traffic | Scale horizontally first |

### Index Strategy

| Query Pattern | Recommended Index |
|---------------|-------------------|
| Find by ID | `{ id: 1 }` (automatic) |
| Find by email (unique) | `{ email: 1 }` with `unique: true` |
| Filter + sort | Compound: `{ region: 1, createdAt: -1 }` |
| Soft delete filter | Include `isDeleted` in compound indexes |

### Memory Considerations

| Practice | Implementation |
|----------|----------------|
| Pagination | Max 100 items per page |
| Projections | Select only needed fields for large documents |
| Streaming | Use `IAsyncEnumerable` for large result sets |
| Pooling | Use `ArrayPool<T>` for temporary buffers |

---

## Quick Reference

### Common Operations

```csharp
// Create
var result = await repo.AddAsync(entity, ct);

// Read by ID
var result = await repo.GetByIdAsync(id, ct);

// Read paged
var result = await repo.GetPagedAsync(new PagedRequest(1, 20), ct);

// Update
entity.UpdateSomething();
var result = await repo.UpdateAsync(entity, ct);

// Delete (soft)
var result = await repo.DeleteAsync(id, ct);

// Transaction
await using var tx = await unitOfWork.BeginTransactionAsync(ct);
await repo1.AddAsync(entity1, ct);
await repo2.AddAsync(entity2, ct);
await tx.CommitAsync(ct);
```

### DI Registration

```csharp
// Program.cs
builder.Services.AddMongoPersistence(options =>
{
    options.ConnectionString = config["MongoDB:ConnectionString"]!;
    options.DatabaseName = "myservice-db";
});

builder.Services.AddMongoRepository<
    Customer,
    CustomerId,
    CustomerDocument,
    CustomerRepository,
    ICustomerRepository>();

builder.Services.AddIndexProvider<CustomerDocument, CustomerIndexProvider>();
```

### Configuration

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb+srv://...",
    "DatabaseName": "myservice-db",
    "ReadPreference": "secondaryPreferred",
    "WriteConcern": "majority",
    "MaxConnectionPoolSize": 100
  }
}
```

---

## Appendix: Technology Choices

| Choice | Alternative Considered | Why We Chose This |
|--------|----------------------|-------------------|
| MongoDB.Driver 3.1.0 | Community drivers | Official, maintained, best support |
| Result<T> monad | Exceptions | Explicit error handling |
| `required` + `init` | Constructor parameters | Less boilerplate, immutable |
| Primary constructors | Traditional constructors | Concise DI |
| Separate audit collection | Embedded history | No 16MB limit, TTL support |
| Soft delete | Hard delete | Audit trail, recovery |
| Secondary reads | Primary only | Cost optimization |
