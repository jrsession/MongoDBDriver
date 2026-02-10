using System.Reflection;
using Company.Persistence.Example.Domain;
using Company.Persistence.Mongo.Mapping;

namespace Company.Persistence.Example.Infrastructure;

/// <summary>
/// Maps between Customer domain entity and CustomerDocument.
/// </summary>
public sealed class CustomerMapper : IEntityMapper<Customer, CustomerId, CustomerDocument>
{
    public CustomerDocument ToDocument(Customer entity) => new()
    {
        Id = FormatId(entity.Id),
        Name = entity.Name,
        Email = entity.Email
    };

    public Customer ToDomain(CustomerDocument document)
    {
        var customer = CreateInstance();

        SetProperty(customer, nameof(Customer.Id), ParseId(document.Id));
        SetProperty(customer, nameof(Customer.Name), document.Name);
        SetProperty(customer, nameof(Customer.Email), document.Email);

        // Audit fields
        SetProperty(customer, nameof(Customer.CreatedAt), document.CreatedAt);
        SetProperty(customer, nameof(Customer.CreatedBy), document.CreatedBy);
        SetProperty(customer, nameof(Customer.UpdatedAt), document.UpdatedAt);
        SetProperty(customer, nameof(Customer.UpdatedBy), document.UpdatedBy);
        SetProperty(customer, nameof(Customer.Version), document.Version);

        // Soft delete fields
        SetProperty(customer, nameof(Customer.IsDeleted), document.IsDeleted);
        SetProperty(customer, nameof(Customer.DeletedAt), document.DeletedAt);
        SetProperty(customer, nameof(Customer.DeletedBy), document.DeletedBy);

        return customer;
    }

    public CustomerId ParseId(string id) => CustomerId.From(id);

    public string FormatId(CustomerId id) => id.ToString();

    private static Customer CreateInstance()
    {
        // Use reflection to call private constructor
        return (Customer)Activator.CreateInstance(typeof(Customer), nonPublic: true)!;
    }

    private static void SetProperty<T>(Customer entity, string propertyName, T value)
    {
        var property = typeof(Customer).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        property?.SetValue(entity, value);
    }
}
