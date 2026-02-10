using Company.Persistence.Abstractions.Entities;

namespace Company.Persistence.Example.Domain;

/// <summary>
/// Customer domain entity with audit support.
/// </summary>
public sealed class Customer : IAuditableEntity<CustomerId>, ISoftDeletable
{
    public CustomerId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    // Audit fields (set by repository)
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public long Version { get; private set; }

    // Soft delete fields (set by repository)
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    private Customer() { }

    public static Customer Create(string name, string email) => new()
    {
        Id = CustomerId.New(),
        Name = name,
        Email = email
    };

    public void Update(string name, string email)
    {
        Name = name;
        Email = email;
    }
}
