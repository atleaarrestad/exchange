using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class SettingsChangeOutboxPublisherWorker(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    ISettingsChangeOutboxPublisher outboxPublisher,
    ILogger<SettingsChangeOutboxPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private readonly string workerId = $"{Environment.MachineName.ToLowerInvariant()}-{Environment.ProcessId}-{Guid.CreateVersion7()}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingEntriesAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed while publishing pending settings outbox entries.");
            }
        }
    }

    private async Task PublishPendingEntriesAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingEntries = await ClaimPendingEntriesAsync(context, cancellationToken);

        if (pendingEntries.Count == 0)
        {
            return;
        }

        foreach (var entry in pendingEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await PublishEntryAsync(entry, cancellationToken);
                entry.PublishedAtUtc = DateTimeOffset.UtcNow;
                entry.LeaseOwnerId = null;
                entry.LeaseExpiresAtUtc = null;
                entry.LeaseToken = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                entry.PublishAttemptCount = checked(entry.PublishAttemptCount + 1);
                entry.LeaseOwnerId = null;
                entry.LeaseExpiresAtUtc = null;
                entry.LeaseToken = null;
                logger.LogError(
                    exception,
                    "Failed publishing settings outbox entry {OutboxEntryId} of type {MessageType}.",
                    entry.Id,
                    entry.MessageType);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<SettingsChangeOutboxEntryEntity>> ClaimPendingEntriesAsync(
        CryptoTransactionsDbContext context,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseExpiresAtUtc = now.Add(LeaseDuration);
        var leaseToken = Guid.CreateVersion7();

        var candidateIds = await context.SettingsChangeOutboxEntries
            .AsNoTracking()
            .Where(entry => entry.PublishedAtUtc == null
                && (entry.LeaseExpiresAtUtc == null || entry.LeaseExpiresAtUtc < now))
            .OrderBy(entry => entry.CreatedAtUtc)
            .Take(100)
            .Select(entry => entry.Id)
            .ToArrayAsync(cancellationToken);

        if (candidateIds.Length == 0)
        {
            return [];
        }

        await context.SettingsChangeOutboxEntries
            .Where(entry => candidateIds.Contains(entry.Id)
                && entry.PublishedAtUtc == null
                && (entry.LeaseExpiresAtUtc == null || entry.LeaseExpiresAtUtc < now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.LeaseOwnerId, workerId)
                    .SetProperty(entry => entry.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(entry => entry.LeaseToken, leaseToken),
                cancellationToken);

        return await context.SettingsChangeOutboxEntries
            .Where(entry => entry.PublishedAtUtc == null && entry.LeaseToken == leaseToken)
            .OrderBy(entry => entry.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    private async Task PublishEntryAsync(SettingsChangeOutboxEntryEntity entry, CancellationToken cancellationToken)
    {
        await outboxPublisher.PublishAsync(entry, cancellationToken);
    }
}
