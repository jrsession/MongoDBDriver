// =============================================================================
// FILE: Entities/IEntity.cs
// PURPOSE: Base identity contract for all domain entities
// =============================================================================

namespace Company.Persistence.Abstractions.Entities;

/// <summary>
/// Base contract for all identifiable entities.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <remarks>
/// <para><b>Design Decision:</b> Generic TId enables strongly-typed IDs (CustomerId, OrderId)
/// to prevent primitive obsession and catch ID mismatches at compile time.</para>
/// <para><b>Covariance:</b> The <c>out</c> modifier allows <c>IEntity&lt;DerivedId&gt;</c>
/// to be used where <c>IEntity&lt;BaseId&gt;</c> is expected.</para>
/// </remarks>
/// <example>
/// <code>
/// // Define a strongly-typed ID
/// public readonly record struct CustomerId(Guid Value);
///
/// // Implement IEntity with the strongly-typed ID
/// public sealed class Customer : IEntity&lt;CustomerId&gt;
/// {
///     public CustomerId Id { get; private init; }
///     public string Name { get; private set; } = string.Empty;
/// }
/// </code>
/// </example>
public interface IEntity<out TId> where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity.
    /// </summary>
    /// <remarks>
    /// The ID is typically set during entity creation and should not change
    /// during the entity's lifetime.
    /// </remarks>
    TId Id { get; }
}
