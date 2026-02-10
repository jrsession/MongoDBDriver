// =============================================================================
// FILE: Configuration/MongoSettings.cs
// PURPOSE: Strongly-typed MongoDB configuration with Atlas-optimized defaults
// =============================================================================

using System.ComponentModel.DataAnnotations;

namespace Company.Persistence.Mongo.Configuration;

/// <summary>
/// Strongly-typed MongoDB connection settings with Atlas-optimized defaults.
/// </summary>
/// <remarks>
/// <para><b>Configuration Section:</b> "MongoDB" in appsettings.json</para>
/// <para><b>Design Decision:</b> Uses <c>required</c> + <c>init</c> to ensure:</para>
/// <list type="bullet">
///   <item>Mandatory properties cannot be null (compile-time safety)</item>
///   <item>Immutable after construction (thread-safe)</item>
///   <item>Clear validation errors if configuration is missing</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // appsettings.json
/// {
///   "MongoDB": {
///     "ConnectionString": "mongodb+srv://user:pass@cluster.mongodb.net",
///     "DatabaseName": "myservice-db",
///     "ReadPreference": "primary"  // Override default for strong consistency
///   }
/// }
///
/// // Registration in Program.cs
/// services.AddOptions&lt;MongoSettings&gt;()
///     .BindConfiguration("MongoDB")
///     .ValidateDataAnnotations()
///     .ValidateOnStart();
/// </code>
/// </example>
public sealed class MongoSettings
{
    /// <summary>
    /// Gets the MongoDB Atlas connection string (mongodb+srv://).
    /// </summary>
    /// <remarks>
    /// <para><b>Security:</b> Should come from Azure Key Vault or AWS Secrets Manager.</para>
    /// <para><b>Never:</b> Commit to source control or log this value.</para>
    /// </remarks>
    [Required(ErrorMessage = "MongoDB:ConnectionString is required")]
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Gets the database name to use.
    /// </summary>
    [Required(ErrorMessage = "MongoDB:DatabaseName is required")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "DatabaseName must be 1-64 characters")]
    public required string DatabaseName { get; init; }

    // =========================================================================
    // TIMEOUT SETTINGS (Atlas-optimized defaults)
    // =========================================================================

    /// <summary>
    /// Gets the connection timeout in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> 10,000ms (10s)</para>
    /// <para><b>Rationale:</b> Atlas serverless/shared tiers can have cold start latency of 5-7s.</para>
    /// </remarks>
    [Range(1000, 60000, ErrorMessage = "ConnectTimeoutMs must be between 1000 and 60000")]
    public int ConnectTimeoutMs { get; init; } = 10_000;

    /// <summary>
    /// Gets the server selection timeout in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> 30,000ms (30s)</para>
    /// <para><b>Rationale:</b> Atlas automatic failover typically takes 10-30s to elect a new primary.</para>
    /// </remarks>
    [Range(5000, 120000, ErrorMessage = "ServerSelectionTimeoutMs must be between 5000 and 120000")]
    public int ServerSelectionTimeoutMs { get; init; } = 30_000;

    /// <summary>
    /// Gets the socket/operation timeout in milliseconds.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> 60,000ms (60s)</para>
    /// <para><b>Rationale:</b> Long-running aggregations need time to complete.</para>
    /// </remarks>
    [Range(5000, 300000, ErrorMessage = "SocketTimeoutMs must be between 5000 and 300000")]
    public int SocketTimeoutMs { get; init; } = 60_000;

    // =========================================================================
    // CONNECTION POOL SETTINGS
    // =========================================================================

    /// <summary>
    /// Gets the maximum connection pool size.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> 100 connections</para>
    /// <para><b>Rationale:</b> Sufficient for most workloads. Atlas M10+ supports up to 1500.</para>
    /// </remarks>
    [Range(10, 1500, ErrorMessage = "MaxConnectionPoolSize must be between 10 and 1500")]
    public int MaxConnectionPoolSize { get; init; } = 100;

    /// <summary>
    /// Gets the minimum connection pool size.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> 5 connections (kept warm)</para>
    /// <para><b>Rationale:</b> Prevents cold start latency on first requests.</para>
    /// </remarks>
    [Range(0, 100, ErrorMessage = "MinConnectionPoolSize must be between 0 and 100")]
    public int MinConnectionPoolSize { get; init; } = 5;

    // =========================================================================
    // READ/WRITE SETTINGS (Cost & Performance Optimized)
    // =========================================================================

    /// <summary>
    /// Gets the read preference for query routing.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> "secondaryPreferred"</para>
    /// <para><b>Options:</b> "primary", "primaryPreferred", "secondary", "secondaryPreferred", "nearest"</para>
    /// <para><b>Cost Impact:</b> Reading from secondaries reduces primary load by ~40-60%,
    /// potentially allowing a smaller (cheaper) Atlas tier.</para>
    /// <para><b>Trade-off:</b> Secondary reads may be milliseconds behind primary (eventual consistency).
    /// Use "primary" for operations requiring strong consistency.</para>
    /// </remarks>
    public string ReadPreference { get; init; } = "secondaryPreferred";

    /// <summary>
    /// Gets the write concern for durability guarantees.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> "majority"</para>
    /// <para><b>Options:</b> "0" (unacknowledged), "1" (primary only), "majority" (replicated)</para>
    /// <para><b>Rationale:</b> "majority" ensures writes survive primary failover and is required for transactions.</para>
    /// <para><b>Trade-off:</b> ~10-20ms additional latency vs "1", but guaranteed durability.</para>
    /// </remarks>
    public string WriteConcern { get; init; } = "majority";

    /// <summary>
    /// Gets the read concern for consistency guarantees.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> "majority"</para>
    /// <para><b>Options:</b> "local", "majority", "linearizable"</para>
    /// <para><b>Rationale:</b> "majority" only reads committed data, avoiding dirty reads.</para>
    /// </remarks>
    public string ReadConcern { get; init; } = "majority";

    // =========================================================================
    // RETRY SETTINGS (Atlas Best Practices)
    // =========================================================================

    /// <summary>
    /// Gets whether to automatically retry writes on transient failures.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> true</para>
    /// <para><b>Rationale:</b> Handles network blips automatically without application code changes.</para>
    /// </remarks>
    public bool RetryWrites { get; init; } = true;

    /// <summary>
    /// Gets whether to automatically retry reads on transient failures.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> true</para>
    /// <para><b>Rationale:</b> Same as RetryWrites for read operations.</para>
    /// </remarks>
    public bool RetryReads { get; init; } = true;

    // =========================================================================
    // AUDIT SETTINGS
    // =========================================================================

    /// <summary>
    /// Gets the name of the audit history collection.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> "_audit_history"</para>
    /// <para>All audit entries are stored in this collection with the entity type as a discriminator.</para>
    /// </remarks>
    public string AuditHistoryCollectionName { get; init; } = "_audit_history";

    /// <summary>
    /// Gets whether to capture command text in OpenTelemetry traces.
    /// </summary>
    /// <remarks>
    /// <para><b>Default:</b> false</para>
    /// <para><b>Security:</b> Enable only in development/test. May expose sensitive data in production.</para>
    /// </remarks>
    public bool CaptureCommandText { get; init; } = false;
}
