// =============================================================================
// FILE: Results/ErrorCodes.cs
// PURPOSE: Well-known error code constants for programmatic handling
// =============================================================================

namespace Company.Persistence.Abstractions.Results;

/// <summary>
/// Well-known error codes for programmatic error handling.
/// </summary>
/// <remarks>
/// <para><b>Design Decision:</b> Constants over enum because:</para>
/// <list type="bullet">
///   <item><b>Extensible:</b> Consumers can define additional codes without modifying this library</item>
///   <item><b>Debuggable:</b> String comparison is clear in debugger and logs</item>
///   <item><b>Serializable:</b> No casting required for JSON/logging</item>
/// </list>
/// <para><b>Usage:</b> Compare against <see cref="Error.Code"/> property:</para>
/// <code>
/// if (result.Error?.Code == ErrorCodes.NotFound)
/// {
///     return NotFound();
/// }
/// </code>
/// </remarks>
/// <example>
/// <code>
/// var result = await repository.GetByIdAsync(id, ct);
///
/// return result switch
/// {
///     { IsSuccess: true } => Ok(result.Value),
///     { Error.Code: ErrorCodes.NotFound } => NotFound(),
///     { Error.Code: ErrorCodes.Conflict } => Conflict(result.Error.Message),
///     _ => Problem(result.Error!.Message)
/// };
/// </code>
/// </example>
public static class ErrorCodes
{
    /// <summary>
    /// The requested entity was not found.
    /// </summary>
    /// <remarks>
    /// Returned when <c>GetByIdAsync</c> cannot find an entity with the specified ID,
    /// or when updating/deleting an entity that doesn't exist.
    /// </remarks>
    public const string NotFound = "NOT_FOUND";

    /// <summary>
    /// Optimistic concurrency conflict (version mismatch).
    /// </summary>
    /// <remarks>
    /// Returned when <c>UpdateAsync</c> detects that the entity was modified
    /// by another process since it was loaded. The caller should reload and retry.
    /// </remarks>
    public const string Conflict = "CONFLICT";

    /// <summary>
    /// Input validation failed.
    /// </summary>
    /// <remarks>
    /// Returned when input parameters don't meet validation requirements.
    /// </remarks>
    public const string Validation = "VALIDATION";

    /// <summary>
    /// Unique constraint violation (duplicate entry).
    /// </summary>
    /// <remarks>
    /// Returned when inserting or updating would violate a unique index
    /// (e.g., duplicate email address).
    /// </remarks>
    public const string Duplicate = "DUPLICATE";

    /// <summary>
    /// User lacks permission for the requested operation.
    /// </summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>
    /// Unexpected internal error occurred.
    /// </summary>
    /// <remarks>
    /// Used for errors that don't fit other categories. The <see cref="Error.InnerException"/>
    /// may contain additional details.
    /// </remarks>
    public const string InternalError = "INTERNAL_ERROR";
}
