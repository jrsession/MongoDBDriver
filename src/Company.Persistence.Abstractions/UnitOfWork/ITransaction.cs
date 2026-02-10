// =============================================================================
// FILE: UnitOfWork/ITransaction.cs
// PURPOSE: Transaction scope interface for multi-document operations
// =============================================================================

namespace Company.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// Represents a transaction scope for multi-document atomic operations.
/// </summary>
/// <remarks>
/// <para><b>When to Use Transactions:</b></para>
/// <list type="bullet">
///   <item>Multiple documents must be created/updated atomically</item>
///   <item>Business invariants span multiple aggregates</item>
///   <item>All-or-nothing semantics are required</item>
/// </list>
/// <para><b>When NOT to Use Transactions:</b></para>
/// <list type="bullet">
///   <item>Single-document operations (MongoDB provides automatic atomicity)</item>
///   <item>Read-only operations</item>
///   <item>Operations that can tolerate eventual consistency</item>
/// </list>
/// <para><b>MongoDB Requirements:</b></para>
/// <list type="bullet">
///   <item>Requires replica set (Atlas always provides this)</item>
///   <item>WriteConcern must be "majority" (our default)</item>
/// </list>
/// <para><b>Usage Pattern:</b></para>
/// <code>
/// await using var tx = await unitOfWork.BeginTransactionAsync(ct);
/// try
/// {
///     await repo1.AddAsync(entity1, ct);
///     await repo2.AddAsync(entity2, ct);
///     await tx.CommitAsync(ct);
/// }
/// catch
/// {
///     await tx.RollbackAsync(ct);
///     throw;
/// }
/// </code>
/// </remarks>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the transaction is still active (not committed or rolled back).
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Commits all operations performed within this transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the transaction has already been committed or rolled back.
    /// </exception>
    /// <remarks>
    /// After calling this method, the transaction is no longer active.
    /// </remarks>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rolls back all operations performed within this transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>A task representing the asynchronous rollback operation.</returns>
    /// <remarks>
    /// <para>After calling this method, the transaction is no longer active.</para>
    /// <para>This method is idempotent - calling it multiple times has no additional effect.</para>
    /// <para>Automatically called on <see cref="IAsyncDisposable.DisposeAsync"/> if
    /// the transaction is still active (i.e., <see cref="CommitAsync"/> was not called).</para>
    /// </remarks>
    Task RollbackAsync(CancellationToken cancellationToken);
}
