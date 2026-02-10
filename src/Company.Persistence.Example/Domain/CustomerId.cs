namespace Company.Persistence.Example.Domain;

/// <summary>
/// Strongly-typed identifier for Customer entities.
/// </summary>
public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public static CustomerId From(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
