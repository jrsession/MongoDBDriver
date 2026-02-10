// =============================================================================
// FILE: UnitOfWork/MongoUnitOfWork.cs
// PURPOSE: MongoDB transaction implementation for multi-document atomicity
// =============================================================================

using Company.Persistence.Abstractions.UnitOfWork;
using Company.Persistence.Mongo.Client;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Company.Persistence.Mongo.UnitOfWork;

/// <summary>
/// MongoDB implementation of IUnitOfWork for opt-in transactions.
/// </summary>
public sealed class MongoUnitOfWork : IUnitOfWork, IDisposable
{
    private readonly IMongoClientProvider _clientProvider;
    private readonly ILogger<MongoUnitOfWork> _logger;
    private IClientSessionHandle? _session;
    private bool _disposed;

    public MongoUnitOfWork(IMongoClientProvider clientProvider, ILogger<MongoUnitOfWork> logger)
    {
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _session?.IsInTransaction ?? false;

    internal IClientSessionHandle? CurrentSession => _session;

    /// <inheritdoc />
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already in progress. Nested transactions are not supported.");
        }

        _logger.LogDebug("Starting MongoDB transaction");

        _session = await _clientProvider.Client
            .StartSessionAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var transactionOptions = new TransactionOptions(
            readConcern: ReadConcern.Majority,
            writeConcern: WriteConcern.WMajority,
            readPreference: ReadPreference.Primary,
            maxCommitTime: TimeSpan.FromSeconds(30));

        _session.StartTransaction(transactionOptions);
        _logger.LogInformation("MongoDB transaction started");

        return new MongoTransaction(_session, _logger, () => _session = null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_session is not null)
        {
            if (_session.IsInTransaction)
            {
                _logger.LogWarning("Disposing UnitOfWork with active transaction. Aborting.");
                try { _session.AbortTransaction(); }
                catch (Exception ex) { _logger.LogError(ex, "Error aborting transaction during dispose"); }
            }
            _session.Dispose();
            _session = null;
        }
    }
}

internal sealed class MongoTransaction : ITransaction
{
    private readonly IClientSessionHandle _session;
    private readonly ILogger _logger;
    private readonly Action _onDispose;
    private bool _completed;
    private bool _disposed;

    public MongoTransaction(IClientSessionHandle session, ILogger logger, Action onDispose)
    {
        _session = session;
        _logger = logger;
        _onDispose = onDispose;
    }

    public bool IsActive => !_completed && !_disposed && _session.IsInTransaction;

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) throw new InvalidOperationException("Transaction already completed.");

        await _session.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
        _logger.LogInformation("MongoDB transaction committed");
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) throw new InvalidOperationException("Transaction already completed.");

        await _session.AbortTransactionAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
        _logger.LogInformation("MongoDB transaction rolled back");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_completed && _session.IsInTransaction)
        {
            _logger.LogWarning("Transaction disposed without commit/rollback. Rolling back.");
            try { await _session.AbortTransactionAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogError(ex, "Error during automatic rollback"); }
        }

        _session.Dispose();
        _onDispose();
    }
}
