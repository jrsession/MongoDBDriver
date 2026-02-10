// =============================================================================
// FILE: Results/Unit.cs
// PURPOSE: Void equivalent for Result<T> (functional unit type)
// =============================================================================

namespace Company.Persistence.Abstractions.Results;

/// <summary>
/// Represents a void return type for <see cref="Result{T}"/>.
/// Used when an operation succeeds but has no meaningful return value.
/// </summary>
/// <remarks>
/// <para><b>Design Decision:</b> Unit type over void because:</para>
/// <list type="bullet">
///   <item><b>Generic compatibility:</b> <c>void</c> cannot be used as a generic type argument</item>
///   <item><b>Consistency:</b> <c>Result&lt;Unit&gt;</c> provides consistent error handling</item>
///   <item><b>Zero allocation:</b> Single static instance avoids heap allocations</item>
/// </list>
/// <para><b>Usage:</b> Return <c>Result&lt;Unit&gt;</c> from operations that don't produce a value:</para>
/// <code>
/// Task&lt;Result&lt;Unit&gt;&gt; DeleteAsync(TId id, CancellationToken ct);
/// </code>
/// </remarks>
/// <example>
/// <code>
/// // Repository method returning Unit
/// public async Task&lt;Result&lt;Unit&gt;&gt; DeleteAsync(CustomerId id, CancellationToken ct)
/// {
///     // ... perform delete ...
///     return Unit.Value;  // Implicit conversion to Result&lt;Unit&gt;.Success
/// }
///
/// // Controller usage
/// var result = await repository.DeleteAsync(id, ct);
/// return result.Match(
///     success: _ => NoContent(),
///     failure: error => Problem(error.Message)
/// );
/// </code>
/// </example>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Gets the singleton Unit value.
    /// </summary>
    /// <remarks>
    /// Always use this static instance instead of creating new Unit values.
    /// </remarks>
    public static readonly Unit Value = default;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>
    /// All Unit values are equal.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// All Unit values are equal.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
