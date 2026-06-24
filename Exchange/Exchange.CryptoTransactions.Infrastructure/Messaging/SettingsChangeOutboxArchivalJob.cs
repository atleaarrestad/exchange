using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Exchange.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class SettingsChangeOutboxArchivalJob(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    SettingsChangeOutboxArchivalOptions options,
    TimeProvider timeProvider) : ICronScheduledJob
{
    public const string Name = "settings-change-outbox-archival";

    public string JobName => Name;
    public string DisplayName => "Settings Change Outbox Archival";
    public string JobType => CronJobTypes.Archival;
    public bool Enabled => options.Enabled;
    public string CronExpression => options.CronExpression;
    public TimeSpan Timeout => options.Timeout;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var archivedCount = await ArchiveBatchAsync(cancellationToken);
            if (archivedCount == 0)
            {
                return;
            }
        }
    }

    private async Task<int> ArchiveBatchAsync(CancellationToken cancellationToken)
    {
        var matureBeforeUtc = timeProvider.GetUtcNow().Subtract(options.MatureAfter);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sourceEntries = await context.SettingsChangeOutboxEntries
            .AsNoTracking()
            .Where(entry => entry.PublishedAtUtc != null && entry.PublishedAtUtc <= matureBeforeUtc)
            .OrderBy(entry => entry.PublishedAtUtc)
            .ThenBy(entry => entry.CreatedAtUtc)
            .Take(options.BatchSize)
            .ToListAsync(cancellationToken);

        if (sourceEntries.Count == 0)
        {
            return 0;
        }

        var archivedAtUtc = timeProvider.GetUtcNow();
        var archiveEntries = sourceEntries
            .Select(entry => new SettingsChangeOutboxArchiveEntryEntity
            {
                Id = entry.Id,
                MessageType = entry.MessageType,
                PayloadJson = entry.PayloadJson,
                CreatedAtUtc = entry.CreatedAtUtc,
                PublishedAtUtc = entry.PublishedAtUtc!.Value,
                PublishAttemptCount = entry.PublishAttemptCount,
                ArchivedAtUtc = archivedAtUtc
            })
            .ToList();

        var sourceIds = sourceEntries.Select(entry => entry.Id).ToArray();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.SettingsChangeOutboxArchiveEntries.AddRange(archiveEntries);
        await context.SaveChangesAsync(cancellationToken);

        var deletedRows = await context.SettingsChangeOutboxEntries
            .Where(entry => sourceIds.Contains(entry.Id)
                && entry.PublishedAtUtc != null
                && entry.PublishedAtUtc <= matureBeforeUtc)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedRows != sourceEntries.Count)
        {
            throw new InvalidOperationException(
                $"Archived {sourceEntries.Count} outbox entries but deleted {deletedRows}. Manual investigation is required.");
        }

        await transaction.CommitAsync(cancellationToken);
        return deletedRows;
    }
}
