using Company.Persistence.Abstractions.Paging;
using Company.Persistence.Abstractions.Results;
using Company.Persistence.Example.Domain;
using Company.Persistence.Example.Infrastructure;
using Company.Persistence.Mongo.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add MongoDB persistence with configuration from appsettings.json
builder.Services.AddMongoPersistence(builder.Configuration, "MongoDB");

// Register mapper and repository
builder.Services.AddSingleton<CustomerMapper>();
builder.Services.AddMongoRepository<Customer, CustomerId, CustomerDocument, CustomerRepository, ICustomerRepository, CustomerMapper>("customers");

// Register index provider
builder.Services.AddIndexProvider<CustomerDocument, CustomerIndexProvider>("customers");

// Add default user context provider (returns "system" for audit fields)
builder.Services.AddDefaultUserContextProvider();

var app = builder.Build();

// Health check endpoint
app.MapHealthChecks("/health");

// Customer API endpoints
var customers = app.MapGroup("/api/customers");

// GET /api/customers - Get all customers (paged)
customers.MapGet("/", async (ICustomerRepository repo, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
{
    var request = new PagedRequest(page, pageSize);
    var result = await repo.GetPagedAsync(request, ct);

    return result.Match(
        success: paged => Results.Ok(new
        {
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages,
            paged.HasNextPage
        }),
        failure: error => Results.Problem(error.Message));
});

// GET /api/customers/{id} - Get customer by ID
customers.MapGet("/{id:guid}", async (Guid id, ICustomerRepository repo, CancellationToken ct) =>
{
    var customerId = new CustomerId(id);
    var result = await repo.GetByIdAsync(customerId, ct);

    return result.Match(
        success: customer => Results.Ok(customer),
        failure: error => error.Code == ErrorCodes.NotFound
            ? Results.NotFound(new { error.Message })
            : Results.Problem(error.Message));
});

// GET /api/customers/by-email/{email} - Get customer by email
customers.MapGet("/by-email/{email}", async (string email, ICustomerRepository repo, CancellationToken ct) =>
{
    var result = await repo.GetByEmailAsync(email, ct);

    return result.Match(
        success: customer => Results.Ok(customer),
        failure: error => error.Code == ErrorCodes.NotFound
            ? Results.NotFound(new { error.Message })
            : Results.Problem(error.Message));
});

// POST /api/customers - Create a new customer
customers.MapPost("/", async (CreateCustomerRequest request, ICustomerRepository repo, CancellationToken ct) =>
{
    var customer = Customer.Create(request.Name, request.Email);
    var result = await repo.AddAsync(customer, ct);

    return result.Match(
        success: created => Results.Created($"/api/customers/{created.Id.Value}", created),
        failure: error => error.Code == ErrorCodes.Duplicate
            ? Results.Conflict(new { error.Message })
            : Results.Problem(error.Message));
});

// PUT /api/customers/{id} - Update a customer
customers.MapPut("/{id:guid}", async (Guid id, UpdateCustomerRequest request, ICustomerRepository repo, CancellationToken ct) =>
{
    var customerId = new CustomerId(id);
    var getResult = await repo.GetByIdAsync(customerId, ct);

    if (getResult.IsFailure)
    {
        return getResult.Error!.Code == ErrorCodes.NotFound
            ? Results.NotFound(new { Message = $"Customer {id} not found" })
            : Results.Problem(getResult.Error.Message);
    }

    var customer = getResult.Value;
    customer.Update(request.Name, request.Email);

    var updateResult = await repo.UpdateAsync(customer, ct);

    return updateResult.Match(
        success: updated => Results.Ok(updated),
        failure: error => error.Code == ErrorCodes.Conflict
            ? Results.Conflict(new { error.Message })
            : Results.Problem(error.Message));
});

// DELETE /api/customers/{id} - Delete a customer (soft delete)
customers.MapDelete("/{id:guid}", async (Guid id, ICustomerRepository repo, CancellationToken ct) =>
{
    var customerId = new CustomerId(id);
    var result = await repo.DeleteAsync(customerId, ct);

    return result.Match(
        success: _ => Results.NoContent(),
        failure: error => error.Code == ErrorCodes.NotFound
            ? Results.NotFound()
            : Results.Problem(error.Message));
});

app.Run();

// Request DTOs
record CreateCustomerRequest(string Name, string Email);
record UpdateCustomerRequest(string Name, string Email);
