// =============================================================================
// FILE: Audit/IUserContextProvider.cs
// PURPOSE: Resolves current user context for audit fields
// =============================================================================

namespace Company.Persistence.Abstractions.Audit;

/// <summary>
/// Provides the current user context for audit tracking.
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> Resolves the current user's identity for populating audit fields
/// (CreatedBy, UpdatedBy, DeletedBy) on entities.</para>
/// <para><b>Design Decision:</b> Interface for testability and flexibility.
/// Implementations can read from various sources:</para>
/// <list type="bullet">
///   <item>HttpContext (ASP.NET Core)</item>
///   <item>JWT claims (API authentication)</item>
///   <item>gRPC metadata</item>
///   <item>Ambient context for background jobs</item>
/// </list>
/// <para><b>Registration:</b> Register as <b>Scoped</b> to capture per-request context:</para>
/// <code>services.AddScoped&lt;IUserContextProvider, HttpContextUserProvider&gt;();</code>
/// </remarks>
/// <example>
/// <code>
/// // ASP.NET Core implementation
/// public sealed class HttpContextUserProvider(IHttpContextAccessor accessor) : IUserContextProvider
/// {
///     public string GetCurrentUserId()
///     {
///         var user = accessor.HttpContext?.User;
///
///         // Try common claim types
///         return user?.FindFirst("sub")?.Value       // OpenID Connect
///             ?? user?.FindFirst("oid")?.Value       // Azure AD
///             ?? user?.FindFirst("user_id")?.Value   // Custom
///             ?? user?.Identity?.Name                // Fallback
///             ?? "system";                           // Background jobs
///     }
///
///     public string? GetCorrelationId()
///     {
///         var context = accessor.HttpContext;
///
///         // Check headers first
///         if (context?.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true)
///             return correlationId.ToString();
///
///         if (context?.Request.Headers.TryGetValue("X-Request-ID", out var requestId) == true)
///             return requestId.ToString();
///
///         // Fall back to OpenTelemetry trace ID
///         return System.Diagnostics.Activity.Current?.TraceId.ToString();
///     }
/// }
///
/// // Background job implementation
/// public sealed class BackgroundJobUserProvider : IUserContextProvider
/// {
///     public string GetCurrentUserId() => "system-job";
///     public string? GetCorrelationId() => System.Diagnostics.Activity.Current?.TraceId.ToString();
/// }
/// </code>
/// </example>
public interface IUserContextProvider
{
    /// <summary>
    /// Gets the current user's identifier.
    /// </summary>
    /// <returns>
    /// The user ID string. Common sources include:
    /// <list type="bullet">
    ///   <item>JWT "sub" claim (OpenID Connect)</item>
    ///   <item>JWT "oid" claim (Azure AD)</item>
    ///   <item>Custom claim type</item>
    ///   <item>"system" or similar for background processes</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>This value is stored in audit fields: CreatedBy, UpdatedBy, DeletedBy.</para>
    /// <para>Should never return null - use a default like "system" or "anonymous"
    /// when no user context is available.</para>
    /// </remarks>
    string GetCurrentUserId();

    /// <summary>
    /// Gets the current correlation/trace ID for distributed tracing.
    /// </summary>
    /// <returns>
    /// The correlation ID from one of these sources (in priority order):
    /// <list type="bullet">
    ///   <item>X-Correlation-ID header</item>
    ///   <item>X-Request-ID header</item>
    ///   <item>OpenTelemetry Activity TraceId</item>
    /// </list>
    /// Returns <c>null</c> if no correlation ID is available.
    /// </returns>
    /// <remarks>
    /// <para>Used for:</para>
    /// <list type="bullet">
    ///   <item>Linking audit entries across services</item>
    ///   <item>Correlating logs with Application Insights traces</item>
    ///   <item>Debugging distributed transactions</item>
    /// </list>
    /// </remarks>
    string? GetCorrelationId();
}
