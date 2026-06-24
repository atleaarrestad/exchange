using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public interface IBackgroundWorkerHeartbeatStore
{
    Task UpsertHeartbeatAsync(string workerName, DateTimeOffset seenAtUtc, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> GetLastSeenAtUtcAsync(string workerName, CancellationToken cancellationToken = default);
}

public sealed class EfCoreBackgroundWorkerHeartbeatStore(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory) : IBackgroundWorkerHeartbeatStore
{
    public async Task UpsertHeartbeatAsync(string workerName, DateTimeOffset seenAtUtc, CancellationToken cancellationToken = default)
    {
        var normalizedWorkerName = NormalizeWorkerName(workerName);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.BackgroundWorkerHeartbeats
            .SingleOrDefaultAsync(candidate => candidate.WorkerName == normalizedWorkerName, cancellationToken);
        if (entity is null)
        {
            context.BackgroundWorkerHeartbeats.Add(new BackgroundWorkerHeartbeatEntity
            {
                WorkerName = normalizedWorkerName,
                LastSeenAtUtc = seenAtUtc
            });
        }
        else
        {
            entity.LastSeenAtUtc = seenAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastSeenAtUtcAsync(string workerName, CancellationToken cancellationToken = default)
    {
        var normalizedWorkerName = NormalizeWorkerName(workerName);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.BackgroundWorkerHeartbeats
            .AsNoTracking()
            .Where(entity => entity.WorkerName == normalizedWorkerName)
            .Select(entity => (DateTimeOffset?)entity.LastSeenAtUtc)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string NormalizeWorkerName(string workerName)
    {
        if (string.IsNullOrWhiteSpace(workerName))
        {
            throw new ArgumentException("Worker name is required.", nameof(workerName));
        }

        return workerName.Trim();
    }
}
