using Company.Persistence.Mongo.Documents;

namespace Company.Persistence.Example.Infrastructure;

public sealed class CustomerDocument : MongoDocumentBase
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
