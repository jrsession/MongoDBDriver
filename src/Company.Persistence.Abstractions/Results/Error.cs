// =============================================================================
// FILE: Results/Error.cs
// PURPOSE: Structured error with code, message, and optional inner exception
// =============================================================================

using System.Diagnostics;

namespace Company.Persistence.Abstractions.Results;

/// <summary>
/// Represents a structured error with a code for programmatic handling
/// and a message for human consumption.
/// </summary>
/// <param name="Code">Machine-readable error code (e.g., "NOT_FOUND"). Use <see cref="ErrorCodes"/> constants.</param>
/// <param name="Message">Human-readable error description suitable for logging or display.</param>
/// <param name="InnerException">Optional underlying exception for infrastructure failures.</param>
/// <remarks>
/// <para><b>Design Decision:</b> Record type provides:</para>
/// <list type="bullet">
///   <item><b>Immutability:</b> Errors cannot be modified after creation</item>
///   <item><b>Value equality:</b> Two errors with same code/message are equal</item>
///   <item><b>Built-in ToString():</b> Automatic formatting for logging</item>
///   <item><b>With expressions:</b> Easy to create variants: <c>error with { Message = "new" }</c></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Using factory methods (recommended)
/// var notFound = Error.NotFound("Customer", customerId);
/// var conflict = Error.ConcurrencyConflict("Customer", customerId);
/// var validation = Error.Validation("Email is required");
///
/// // Creating custom errors
/// var custom = new Error("CUSTOM_CODE", "Custom message");
///
/// // From exception (for infrastructure errors)
/// try { ... }
/// catch (Exception ex)
/// {
///     return Error.FromException(ex);
/// }
/// </code>
/// </example>
[DebuggerDisplay("{Code}: {Message}")]
public sealed record Error(string Code, string Message, Exception? InnerException = null)
{
    /// <summary>
    /// Creates a "not found" error for the specified entity type and ID.
    /// </summary>
    /// <param name="entityType">The type of entity (e.g., "Customer").</param>
    /// <param name="id">The ID that was not found.</param>
    /// <returns>An error with code <see cref="ErrorCodes.NotFound"/>.</returns>
    /// <example>
    /// <code>
    /// return Error.NotFound("Customer", customerId);
    /// // Code: "NOT_FOUND"
    /// // Message: "Customer with ID '123' was not found."
    /// </code>
    /// </example>
    public static Error NotFound(string entityType, object id) =>
        new(ErrorCodes.NotFound, $"{entityType} with ID '{id}' was not found.");

    /// <summary>
    /// Creates a "not found" error using the entity type name from the generic parameter.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="id">The ID that was not found.</param>
    /// <returns>An error with code <see cref="ErrorCodes.NotFound"/>.</returns>
    /// <example>
    /// <code>
    /// return Error.NotFound&lt;Customer&gt;(customerId);
    /// // Code: "NOT_FOUND"
    /// // Message: "Customer with ID '123' was not found."
    /// </code>
    /// </example>
    public static Error NotFound<TEntity>(object id) =>
        NotFound(typeof(TEntity).Name, id);

    /// <summary>
    /// Creates a generic conflict error.
    /// </summary>
    /// <param name="message">Description of the conflict.</param>
    /// <returns>An error with code <see cref="ErrorCodes.Conflict"/>.</returns>
    public static Error Conflict(string message) =>
        new(ErrorCodes.Conflict, message);

    /// <summary>
    /// Creates a concurrency conflict error for optimistic locking failures.
    /// </summary>
    /// <param name="entityType">The type of entity.</param>
    /// <param name="id">The entity ID.</param>
    /// <returns>An error with code <see cref="ErrorCodes.Conflict"/>.</returns>
    /// <example>
    /// <code>
    /// return Error.ConcurrencyConflict("Customer", customerId);
    /// // Code: "CONFLICT"
    /// // Message: "Customer with ID '123' was modified by another process. Please retry."
    /// </code>
    /// </example>
    public static Error ConcurrencyConflict(string entityType, object id) =>
        new(ErrorCodes.Conflict, $"{entityType} with ID '{id}' was modified by another process. Please retry.");

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    /// <param name="message">Description of the validation failure.</param>
    /// <returns>An error with code <see cref="ErrorCodes.Validation"/>.</returns>
    public static Error Validation(string message) =>
        new(ErrorCodes.Validation, message);

    /// <summary>
    /// Creates a duplicate/unique constraint error.
    /// </summary>
    /// <param name="message">Description of the duplicate.</param>
    /// <returns>An error with code <see cref="ErrorCodes.Duplicate"/>.</returns>
    /// <example>
    /// <code>
    /// return Error.Duplicate("A customer with this email already exists.");
    /// </code>
    /// </example>
    public static Error Duplicate(string message) =>
        new(ErrorCodes.Duplicate, message);

    /// <summary>
    /// Creates an unauthorized access error.
    /// </summary>
    /// <param name="message">Description of the access denial.</param>
    /// <returns>An error with code <see cref="ErrorCodes.Unauthorized"/>.</returns>
    public static Error Unauthorized(string message) =>
        new(ErrorCodes.Unauthorized, message);

    /// <summary>
    /// Creates an error from an exception (for infrastructure failures).
    /// </summary>
    /// <param name="exception">The underlying exception.</param>
    /// <returns>An error with code <see cref="ErrorCodes.InternalError"/>.</returns>
    /// <remarks>
    /// Use this for unexpected infrastructure errors that should be logged
    /// but not exposed directly to callers.
    /// </remarks>
    public static Error FromException(Exception exception) =>
        new(ErrorCodes.InternalError, exception.Message, exception);
}
