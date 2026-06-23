using System.Text.Json;
using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class SqliteCryptoTransferIdempotencyStore : ICryptoTransferIdempotencyStore
{
    private const string PendingReceiptMarker = "";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan PendingLeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxPendingWaitDuration = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public SqliteCryptoTransferIdempotencyStore(string connectionString)
        : this(CreateFactory(connectionString))
    {
    }

    internal SqliteCryptoTransferIdempotencyStore(IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<CryptoTransferReceipt> ExecuteOnceAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        string requestFingerprint,
        Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ArgumentNullException.ThrowIfNull(transferFactory);

        await EnsureInitializedAsync(cancellationToken);

        var normalizedSourceAccountId = sourceAccountId.Trim();
        var normalizedIdempotencyKey = idempotencyKey.Trim();
        var normalizedRequestFingerprint = requestFingerprint.Trim();
        var waitStartedAtUtc = DateTimeOffset.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existingRecord = await GetExistingRecordAsync(
                normalizedSourceAccountId,
                assetSymbol,
                normalizedIdempotencyKey,
                cancellationToken);

            if (existingRecord is not null)
            {
                EnsureMatchingRequestFingerprint(existingRecord, normalizedRequestFingerprint);
            }

            if (existingRecord is not null && existingRecord.Status == IdempotencyStatus.Completed)
            {
                return DeserializeReceipt(existingRecord.ReceiptJson);
            }

            if (existingRecord is null)
            {
                if (await TryInsertPendingRecordAsync(
                        normalizedSourceAccountId,
                        assetSymbol,
                        normalizedIdempotencyKey,
                        normalizedRequestFingerprint,
                        cancellationToken))
                {
                    return await ExecuteOwnedPendingAsync(
                        normalizedSourceAccountId,
                        assetSymbol,
                        normalizedIdempotencyKey,
                        normalizedRequestFingerprint,
                        transferFactory,
                        cancellationToken);
                }
            }
            else if (existingRecord.Status == IdempotencyStatus.Pending &&
                     DateTimeOffset.UtcNow - existingRecord.CreatedAtUtc.ToUniversalTime() > PendingLeaseDuration)
            {
                throw new IdempotencyOperationPendingException(
                    $"Idempotency key '{existingRecord.IdempotencyKey}' has a pending transfer in an unknown state. " +
                    "Automatic replay is blocked to avoid duplicate transfers.");
            }
            else if (existingRecord is not null && existingRecord.Status == IdempotencyStatus.Pending &&
                     DateTimeOffset.UtcNow - waitStartedAtUtc > MaxPendingWaitDuration)
            {
                throw new IdempotencyOperationPendingException(
                    $"Idempotency key '{existingRecord.IdempotencyKey}' is still being processed. " +
                    "Retry the same request with the same idempotency key.");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async Task<CryptoTransferReceipt> ExecuteOwnedPendingAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        string requestFingerprint,
        Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
        CancellationToken cancellationToken)
    {
        var waitStartedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            var createdReceipt = await transferFactory(cancellationToken);
            var completed = await TryMarkCompletedAsync(
                sourceAccountId,
                assetSymbol,
                idempotencyKey,
                createdReceipt,
                cancellationToken);

            if (completed)
            {
                return createdReceipt;
            }
        }
        catch (Exception exception) when (
            exception is BlockchainTransferRejectedException
            or InsufficientFundsException
            or ExternalDependencyNotConfiguredException)
        {
            await TryReleasePendingRecordAsync(sourceAccountId, assetSymbol, idempotencyKey, cancellationToken);
            throw;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existingRecord = await GetExistingRecordAsync(sourceAccountId, assetSymbol, idempotencyKey, cancellationToken);
            if (existingRecord is not null && existingRecord.Status == IdempotencyStatus.Completed)
            {
                EnsureMatchingRequestFingerprint(existingRecord, requestFingerprint);
                return DeserializeReceipt(existingRecord.ReceiptJson);
            }

            if (existingRecord is not null)
            {
                EnsureMatchingRequestFingerprint(existingRecord, requestFingerprint);
            }

            if (DateTimeOffset.UtcNow - waitStartedAtUtc > MaxPendingWaitDuration)
            {
                throw new IdempotencyOperationPendingException(
                    $"Idempotency key '{idempotencyKey}' is still being processed. Retry the same request with the same idempotency key.");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async Task<CryptoTransferIdempotencyReceiptEntity?> GetExistingRecordAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CryptoTransferIdempotencyReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.SourceAccountId == sourceAccountId
                        && receipt.AssetSymbol == assetSymbol.Value
                        && receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private async Task<bool> TryInsertPendingRecordAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.CryptoTransferIdempotencyReceipts.Add(new CryptoTransferIdempotencyReceiptEntity
        {
            SourceAccountId = sourceAccountId,
            AssetSymbol = assetSymbol.Value,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            ReceiptJson = PendingReceiptMarker,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = IdempotencyStatus.Pending
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return false;
        }
    }

    private async Task<bool> TryMarkCompletedAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CryptoTransferReceipt receipt,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var affectedRows = await context.CryptoTransferIdempotencyReceipts
            .Where(record => record.SourceAccountId == sourceAccountId
                          && record.AssetSymbol == assetSymbol.Value
                          && record.IdempotencyKey == idempotencyKey
                          && record.Status == IdempotencyStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.ReceiptJson, JsonSerializer.Serialize(receipt, JsonOptions))
                .SetProperty(record => record.CreatedAtUtc, DateTimeOffset.UtcNow)
                .SetProperty(record => record.Status, IdempotencyStatus.Completed), cancellationToken);

        return affectedRows == 1;
    }

    private async Task TryReleasePendingRecordAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.CryptoTransferIdempotencyReceipts
            .Where(record => record.SourceAccountId == sourceAccountId
                          && record.AssetSymbol == assetSymbol.Value
                          && record.IdempotencyKey == idempotencyKey
                          && record.Status == IdempotencyStatus.Pending)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static CryptoTransferReceipt DeserializeReceipt(string serializedReceipt)
    {
        var receipt = JsonSerializer.Deserialize<CryptoTransferReceipt>(serializedReceipt, JsonOptions);
        return receipt ?? throw new InvalidOperationException("Stored idempotency receipt could not be deserialized.");
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (isInitialized)
            {
                return;
            }

            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private static IDbContextFactory<CryptoTransactionsDbContext> CreateFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        var options = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
            .UseSqlite(connectionString.Trim())
            .Options;

        return new LocalDbContextFactory(options);
    }

    private sealed class LocalDbContextFactory(DbContextOptions<CryptoTransactionsDbContext> options)
        : IDbContextFactory<CryptoTransactionsDbContext>
    {
        public CryptoTransactionsDbContext CreateDbContext() => new(options);

        public ValueTask<CryptoTransactionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        const int SqliteConstraintErrorCode = 19;
        const int SqlitePrimaryKeyConstraintExtendedCode = 1555;
        const int SqliteUniqueConstraintExtendedCode = 2067;

        return sqliteException.SqliteErrorCode == SqliteConstraintErrorCode &&
               (sqliteException.SqliteExtendedErrorCode == SqlitePrimaryKeyConstraintExtendedCode ||
                sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraintExtendedCode);
    }

    private static void EnsureMatchingRequestFingerprint(
        CryptoTransferIdempotencyReceiptEntity existingRecord,
        string requestFingerprint)
    {
        if (string.Equals(existingRecord.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        throw new IdempotencyKeyConflictException(
            $"Idempotency key '{existingRecord.IdempotencyKey}' was already used with a different transfer request.");
    }
}
