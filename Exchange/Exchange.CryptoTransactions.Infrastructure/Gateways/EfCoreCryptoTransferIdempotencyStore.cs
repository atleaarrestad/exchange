using System.Text.Json;
using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreCryptoTransferIdempotencyStore : ICryptoTransferIdempotencyStore
{
    private const string PendingReceiptMarker = "";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan PendingLeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxPendingWaitDuration = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public EfCoreCryptoTransferIdempotencyStore(string connectionString)
        : this(CreateFactory(connectionString))
    {
    }

    internal EfCoreCryptoTransferIdempotencyStore(IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<CryptoTransferIdempotencyRegistration> RegisterPendingAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        string requestFingerprint,
        decimal totalDebit,
        string destinationAddress,
        decimal amount,
        decimal networkFee,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationAddress);
        if (totalDebit <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDebit), totalDebit, "Total debit must be greater than zero.");
        }

        await EnsureInitializedAsync(cancellationToken);

        var normalizedSourceAccountId = sourceAccountId.Trim();
        var normalizedIdempotencyKey = idempotencyKey.Trim();
        var normalizedRequestFingerprint = requestFingerprint.Trim();
        var normalizedDestinationAddress = destinationAddress.Trim();

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
                if (existingRecord.Status == IdempotencyStatus.Completed)
                {
                    return new CryptoTransferIdempotencyRegistration(
                        CreatedPending: false,
                        CompletedReceipt: DeserializeReceipt(existingRecord.ReceiptJson));
                }

                return new CryptoTransferIdempotencyRegistration(
                    CreatedPending: false,
                    CompletedReceipt: null);
            }

            var created = await TryInsertPendingRecordAsync(
                normalizedSourceAccountId,
                assetSymbol,
                normalizedIdempotencyKey,
                normalizedRequestFingerprint,
                totalDebit,
                normalizedDestinationAddress,
                amount,
                networkFee,
                cancellationToken);
            if (created)
            {
                return new CryptoTransferIdempotencyRegistration(
                    CreatedPending: true,
                    CompletedReceipt: null);
            }
        }
    }

    public async Task<CryptoTransferReceipt> ExecuteOnceAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        string requestFingerprint,
        decimal totalDebit,
        Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        if (totalDebit <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDebit), totalDebit, "Total debit must be greater than zero.");
        }
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
                        totalDebit,
                        destinationAddress: "legacy-execute-once",
                        amount: totalDebit,
                        networkFee: 0m,
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
                     DateTimeOffset.UtcNow - existingRecord.LastUpdatedAtUtc.ToUniversalTime() > PendingLeaseDuration)
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

    public async Task<IReadOnlyList<PendingCryptoTransferOperation>> GetPendingOlderThanAsync(
        DateTimeOffset olderThanUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var normalizedOlderThanUtc = olderThanUtc.ToUniversalTime();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await context.CryptoTransferIdempotencyReceipts
            .AsNoTracking()
            .Where(record => record.Status == IdempotencyStatus.Pending &&
                             record.LastUpdatedAtUtc <= normalizedOlderThanUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(record => new PendingCryptoTransferOperation(
                record.SourceAccountId,
                AssetSymbol.Parse(record.AssetSymbol),
                record.IdempotencyKey,
                record.RequestFingerprint,
                record.TotalDebit,
                record.DestinationAddress,
                record.Amount,
                record.NetworkFee,
                record.CreatedAtUtc,
                record.LastUpdatedAtUtc))
            .ToArray();
    }

    public async Task<bool> TryMarkCompletedAsync(
        PendingCryptoTransferOperation operation,
        CryptoTransferReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(receipt);
        await EnsureInitializedAsync(cancellationToken);
        return await TryMarkCompletedPendingRecordAsync(operation, receipt, cancellationToken);
    }

    public async Task<bool> TryAcquirePendingAsync(
        PendingCryptoTransferOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await EnsureInitializedAsync(cancellationToken);
        return await TryAcquirePendingRecordAsync(operation, cancellationToken);
    }

    public async Task<bool> TryReleasePendingAsync(
        PendingCryptoTransferOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await EnsureInitializedAsync(cancellationToken);
        return await TryReleasePendingRecordAsync(operation, cancellationToken);
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
            var completed = await TryMarkCompletedPendingRecordAsync(
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
            or ApplicationValidationException
            or ArgumentException
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
        decimal totalDebit,
        string destinationAddress,
        decimal amount,
        decimal networkFee,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.CryptoTransferIdempotencyReceipts.Add(new CryptoTransferIdempotencyReceiptEntity
        {
            SourceAccountId = sourceAccountId,
            AssetSymbol = assetSymbol.Value,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            TotalDebit = totalDebit,
            DestinationAddress = destinationAddress,
            Amount = amount,
            NetworkFee = networkFee,
            ReceiptJson = PendingReceiptMarker,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
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

    private Task<bool> TryMarkCompletedPendingRecordAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CryptoTransferReceipt receipt,
        CancellationToken cancellationToken) =>
        TryMarkCompletedPendingRecordAsync(
            new PendingCryptoTransferOperation(
                sourceAccountId,
                assetSymbol,
                idempotencyKey,
                RequestFingerprint: string.Empty,
                TotalDebit: receipt.TotalDebit,
                DestinationAddress: string.Empty,
                Amount: 0m,
                NetworkFee: 0m,
                CreatedAtUtc: DateTimeOffset.MinValue,
                LastUpdatedAtUtc: DateTimeOffset.MinValue),
            receipt,
            cancellationToken);

    private async Task<bool> TryMarkCompletedPendingRecordAsync(
        PendingCryptoTransferOperation operation,
        CryptoTransferReceipt receipt,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var affectedRows = await context.CryptoTransferIdempotencyReceipts
            .Where(record => record.SourceAccountId == operation.SourceAccountId
                          && record.AssetSymbol == operation.AssetSymbol.Value
                          && record.IdempotencyKey == operation.IdempotencyKey
                          && (string.IsNullOrEmpty(operation.RequestFingerprint) || record.RequestFingerprint == operation.RequestFingerprint)
                          && record.Status == IdempotencyStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.ReceiptJson, JsonSerializer.Serialize(receipt, JsonOptions))
                .SetProperty(record => record.LastUpdatedAtUtc, DateTimeOffset.UtcNow)
                .SetProperty(record => record.Status, IdempotencyStatus.Completed), cancellationToken);

        return affectedRows == 1;
    }

    private async Task<bool> TryAcquirePendingRecordAsync(
        PendingCryptoTransferOperation operation,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var affectedRows = await context.CryptoTransferIdempotencyReceipts
            .Where(record => record.SourceAccountId == operation.SourceAccountId
                          && record.AssetSymbol == operation.AssetSymbol.Value
                          && record.IdempotencyKey == operation.IdempotencyKey
                          && record.RequestFingerprint == operation.RequestFingerprint
                          && record.Status == IdempotencyStatus.Pending
                          && record.LastUpdatedAtUtc == operation.LastUpdatedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.LastUpdatedAtUtc, DateTimeOffset.UtcNow), cancellationToken);

        return affectedRows == 1;
    }

    private Task<bool> TryReleasePendingRecordAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        TryReleasePendingRecordAsync(
            new PendingCryptoTransferOperation(
                sourceAccountId,
                assetSymbol,
                idempotencyKey,
                RequestFingerprint: string.Empty,
                TotalDebit: 0m,
                DestinationAddress: string.Empty,
                Amount: 0m,
                NetworkFee: 0m,
                CreatedAtUtc: DateTimeOffset.MinValue,
                LastUpdatedAtUtc: DateTimeOffset.MinValue),
            cancellationToken);

    private async Task<bool> TryReleasePendingRecordAsync(
        PendingCryptoTransferOperation operation,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var affectedRows = await context.CryptoTransferIdempotencyReceipts
            .Where(record => record.SourceAccountId == operation.SourceAccountId
                          && record.AssetSymbol == operation.AssetSymbol.Value
                          && record.IdempotencyKey == operation.IdempotencyKey
                          && (string.IsNullOrEmpty(operation.RequestFingerprint) || record.RequestFingerprint == operation.RequestFingerprint)
                          && record.Status == IdempotencyStatus.Pending)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 1;
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
            .UseNpgsql(connectionString.Trim())
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
        if (exception.InnerException is null)
        {
            return false;
        }

        var exceptionType = exception.InnerException.GetType();

        var sqlState = exceptionType.GetProperty("SqlState")?.GetValue(exception.InnerException) as string;
        if (string.Equals(sqlState, "23505", StringComparison.Ordinal))
        {
            return true;
        }

        var message = exception.InnerException.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase);
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
