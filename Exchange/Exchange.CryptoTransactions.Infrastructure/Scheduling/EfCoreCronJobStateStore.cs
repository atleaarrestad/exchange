using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Exchange.Infrastructure.Scheduling;
using Exchange.Infrastructure.Scheduling.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Scheduling;

public sealed class EfCoreCronJobStateStore(IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory) : ICronJobStateStore
{
    public async Task EnsureStateAsync(string jobName, string cronExpression, bool isEnabled, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await context.CronJobStates.SingleOrDefaultAsync(entity => entity.JobName == jobName, cancellationToken);

        if (state is null)
        {
            context.CronJobStates.Add(new CronJobStateEntity
            {
                JobName = jobName,
                CronExpression = cronExpression,
                IsEnabled = isEnabled,
                NextRunAtUtc = CronJobRunnerOptions.GetNextOccurrenceUtc(cronExpression, nowUtc)
            });
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (state.CronExpression == cronExpression && state.IsEnabled == isEnabled)
        {
            return;
        }

        state.CronExpression = cronExpression;
        state.IsEnabled = isEnabled;
        state.NextRunAtUtc = CronJobRunnerOptions.GetNextOccurrenceUtc(cronExpression, nowUtc);
        state.LeaseOwnerId = null;
        state.LeaseExpiresAtUtc = null;
        state.LeaseToken = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CronJobLeaseClaim?> TryClaimAsync(
        string jobName,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var leaseToken = Guid.CreateVersion7();
        var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var claimedRows = await context.CronJobStates
            .Where(state => state.JobName == jobName
                && state.IsEnabled
                && state.NextRunAtUtc <= nowUtc
                && (state.LeaseExpiresAtUtc == null || state.LeaseExpiresAtUtc < nowUtc))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(state => state.LeaseOwnerId, workerId)
                    .SetProperty(state => state.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(state => state.LeaseToken, leaseToken),
                cancellationToken);

        if (claimedRows == 0)
        {
            return null;
        }

        var claimedState = await context.CronJobStates
            .AsNoTracking()
            .SingleAsync(state => state.JobName == jobName && state.LeaseToken == leaseToken, cancellationToken);
        return new CronJobLeaseClaim(leaseToken, claimedState.NextRunAtUtc);
    }

    public async Task<Guid> StartExecutionAsync(
        string jobName,
        string workerId,
        DateTimeOffset scheduledAtUtc,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runRecordId = Guid.CreateVersion7();
        context.CronJobExecutionRecords.Add(new CronJobExecutionRecordEntity
        {
            Id = runRecordId,
            JobName = jobName,
            RunnerId = workerId,
            ScheduledAtUtc = scheduledAtUtc,
            StartedAtUtc = startedAtUtc,
            Status = CronJobRunStatus.Running
        });
        await context.SaveChangesAsync(cancellationToken);
        return runRecordId;
    }

    public async Task CompleteExecutionAsync(
        Guid runRecordId,
        string jobName,
        Guid leaseToken,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        DateTimeOffset nextRunAtUtc,
        CronJobRunStatus status,
        string resultMessage,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await context.CronJobStates
            .SingleAsync(entry => entry.JobName == jobName && entry.LeaseToken == leaseToken, cancellationToken);
        state.LastStartedAtUtc = startedAtUtc;
        state.LastCompletedAtUtc = completedAtUtc;
        state.LastRunStatus = status;
        state.LastError = error;
        state.NextRunAtUtc = nextRunAtUtc;
        state.LeaseOwnerId = null;
        state.LeaseExpiresAtUtc = null;
        state.LeaseToken = null;

        var runRecord = await context.CronJobExecutionRecords
            .SingleAsync(entry => entry.Id == runRecordId, cancellationToken);
        runRecord.CompletedAtUtc = completedAtUtc;
        runRecord.Status = status;
        runRecord.ResultMessage = resultMessage;
        runRecord.Error = error;

        await context.SaveChangesAsync(cancellationToken);
    }
}
