// =============================================================================
// FILE: UnitOfWork/IUnitOfWork.cs
// PURPOSE: Transaction factory for opt-in multi-document transactions
// =============================================================================

namespace Company.Persistence.Abstractions.UnitOfWork;

/// <summary>
/// Factory for creating transaction scopes.
/// </summary>
/// <remarks>
/// <para><b>Design Decision:</b> Transactions are opt-in, not automatic, because:</para>
/// <list type="bullet">
///   <item><b>Performance:</b> Transactions add latency (~10-20ms overhead)</item>
///   <item><b>Simplicity:</b> Most operations affect single documents (already atomic)</item>
///   <item><b>Scalability:</b> Long-running transactions can cause lock contention</item>
/// </list>
/// <para><b>When to Use:</b></para>
/// <list type="bullet">
///   <item>Customer + initial Subscription must be created together</item>
///   <item>Order + OrderItems must be consistent</item>
///   <item>Transfer funds between two accounts</item>
/// </list>
/// <para><b>Registration:</b> Register as <b>Scoped</b> for per-request transaction management:</para>
/// <code>services.AddScoped&lt;IUnitOfWork, MongoUnitOfWork&gt;();</code>
/// </remarks>
/// <example>
/// <code>
/// public class CustomerOnboardingService(
///     IUnitOfWork unitOfWork,
///     IRepository&lt;Customer, CustomerId&gt; customerRepo,
///     IRepository&lt;Subscription, SubscriptionId&gt; subscriptionRepo)
/// {
///     public async Task&lt;Result&lt;Customer&gt;&gt; OnboardAsync(
///         CreateCustomerCommand cmd,
///         CancellationToken ct)
///     {
///         // Start a transaction
///         await using var tx = await unitOfWork.BeginTransactionAsync(ct);
///
///         try
///         {
///             // Create customer
///             var customer = Customer.Create(cmd.Name, cmd.Email);
///             var customerResult = await customerRepo.AddAsync(customer, ct);
///
///             if (!customerResult.IsSuccess)
///             {
///                 await tx.RollbackAsync(ct);
///                 return customerResult;
///             }
///
///             // Create subscription
///             var subscription = Subscription.CreateTrial(customer.Id);
///             var subResult = await subscriptionRepo.AddAsync(subscription, ct);
///
///             if (!subResult.IsSuccess)
///             {
///                 await tx.RollbackAsync(ct);
///                 return Result.Failure&lt;Customer&gt;(subResult.Error!);
///             }
///
///             // Commit both operations atomically
///             await tx.CommitAsync(ct);
///             return customerResult;
///         }
///         catch (Exception)
///         {
///             await tx.RollbackAsync(ct);
///             throw;
///         }
///     }
/// }
/// </code>
/// </example>
public interface IUnitOfWork
{
    /// <summary>
    /// Begins a new transaction scope.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token (required).</param>
    /// <returns>
    /// A transaction scope that must be committed or rolled back.
    /// Use <c>await using</c> to ensure proper cleanup.
    /// </returns>
    /// <remarks>
    /// <para><b>Important:</b> All repository operations within the transaction scope
    /// will automatically participate in the transaction.</para>
    /// <para><b>Cleanup:</b> If the transaction is disposed without calling
    /// <see cref="ITransaction.CommitAsync"/>, it is automatically rolled back.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Recommended pattern using await using
    /// await using var tx = await unitOfWork.BeginTransactionAsync(ct);
    /// // ... operations ...
    /// await tx.CommitAsync(ct);
    /// // tx is automatically disposed even if an exception occurs
    /// </code>
    /// </example>
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets whether a transaction is currently active for this unit of work instance.
    /// </summary>
    /// <remarks>
    /// Use this to check if operations will participate in a transaction.
    /// </remarks>
    bool HasActiveTransaction { get; }
}
