using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Exchange.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Scheduling;

public sealed class EfCoreCronJobsAdminService(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    ICronJobStateStore stateStore,
    IEnumerable<ICronScheduledJob> jobs,
    CronJobRunnerOptions runnerOptions,
    TimeProvider timeProvider)
    : ICronJobsAdminService
{
    private const int MaxExecutionLogsPerJob = 100;
    private readonly string runnerId = $"admin-{Environment.MachineName.ToLowerInvariant()}-{Environment.ProcessId}-{Guid.CreateVersion7()}";
    private readonly IReadOnlyDictionary<string, ICronScheduledJob> jobsByName = jobs
        .GroupBy(job => job.JobName, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

    public async Task<IReadOnlyList<CronJobAdminSummary>> GetAllAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        foreach (var job in jobsByName.Values)
        {
            await stateStore.EnsureStateAsync(job.JobName, job.CronExpression, job.Enabled, nowUtc, cancellationToken);
        }

        var jobNames = jobsByName.Keys.ToArray();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var states = (await context.CronJobStates
            .AsNoTracking()
            .Where(state => jobNames.Contains(state.JobName))
            .ToListAsync(cancellationToken))
            .ToDictionary(state => state.JobName, StringComparer.Ordinal);

        var result = new List<CronJobAdminSummary>(jobsByName.Count);
        foreach (var job in jobsByName.Values.OrderBy(job => job.JobName, StringComparer.Ordinal))
        {
            if (!states.TryGetValue(job.JobName, out var state))
            {
                continue;
            }

            result.Add(new CronJobAdminSummary(
                job.JobName,
                job.DisplayName,
                string.IsNullOrWhiteSpace(job.JobType) ? CronJobTypes.General : job.JobType,
                job.Enabled,
                job.CronExpression,
                Math.Max(1, (int)Math.Ceiling(job.Timeout.TotalSeconds)),
                state.NextRunAtUtc,
                state.LastStartedAtUtc,
                state.LastCompletedAtUtc,
                state.LastRunStatus?.ToString(),
                state.LastError));
        }

        return result;
    }

    public async Task<IReadOnlyList<CronJobExecutionRecordSummary>?> GetExecutionsAsync(string jobName, CancellationToken cancellationToken)
    {
        if (!jobsByName.ContainsKey(jobName))
        {
            return null;
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var executionRecords = await context.CronJobExecutionRecords
            .AsNoTracking()
            .Where(record => record.JobName == jobName)
            .OrderByDescending(record => record.StartedAtUtc)
            .Take(MaxExecutionLogsPerJob)
            .ToListAsync(cancellationToken);

        return executionRecords.Select(MapExecutionRecord).ToArray();
    }

    public async Task<CronJobManualRunResult?> RunNowAsync(string jobName, CancellationToken cancellationToken)
    {
        if (!jobsByName.TryGetValue(jobName, out var job))
        {
            return null;
        }

        if (!job.Enabled)
        {
            throw new CronJobRunRejectedException($"Cron job '{jobName}' is disabled.");
        }

        var nowUtc = timeProvider.GetUtcNow();
        await stateStore.EnsureStateAsync(job.JobName, job.CronExpression, job.Enabled, nowUtc, cancellationToken);

        await using (var context = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var rows = await context.CronJobStates
                .Where(state => state.JobName == job.JobName)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(state => state.CronExpression, job.CronExpression)
                        .SetProperty(state => state.IsEnabled, job.Enabled)
                        .SetProperty(state => state.NextRunAtUtc, nowUtc)
                        .SetProperty(state => state.LeaseOwnerId, (string?)null)
                        .SetProperty(state => state.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                        .SetProperty(state => state.LeaseToken, (Guid?)null),
                    cancellationToken);

            if (rows == 0)
            {
                return null;
            }
        }

        var minimumLeaseDuration = job.Timeout + TimeSpan.FromSeconds(30);
        var leaseDuration = runnerOptions.LeaseDuration >= minimumLeaseDuration
            ? runnerOptions.LeaseDuration
            : minimumLeaseDuration;
        var claim = await stateStore.TryClaimAsync(job.JobName, runnerId, nowUtc, leaseDuration, cancellationToken);
        if (claim is null)
        {
            throw new CronJobRunRejectedException($"Cron job '{job.JobName}' is already running.");
        }

        var startedAtUtc = timeProvider.GetUtcNow();
        var runRecordId = await stateStore.StartExecutionAsync(
            job.JobName,
            runnerId,
            claim.ScheduledAtUtc,
            startedAtUtc,
            cancellationToken);

        var completedAtUtc = startedAtUtc;
        var status = CronJobRunStatus.Succeeded;
        var resultMessage = "Completed";
        string? error = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(job.Timeout);
            await job.ExecuteAsync(timeoutCts.Token);
            completedAtUtc = timeProvider.GetUtcNow();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            completedAtUtc = timeProvider.GetUtcNow();
            status = CronJobRunStatus.TimedOut;
            resultMessage = $"Timed out after {job.Timeout.TotalSeconds:0} seconds";
            error = resultMessage;
        }
        catch (Exception exception)
        {
            completedAtUtc = timeProvider.GetUtcNow();
            status = CronJobRunStatus.Failed;
            resultMessage = "Failed";
            error = exception.ToString();
        }

        var nextRunAtUtc = CronJobRunnerOptions.GetNextOccurrenceUtc(job.CronExpression, completedAtUtc);
        await stateStore.CompleteExecutionAsync(
            runRecordId,
            job.JobName,
            claim.LeaseToken,
            startedAtUtc,
            completedAtUtc,
            nextRunAtUtc,
            status,
            resultMessage,
            error,
            cancellationToken);

        return new CronJobManualRunResult(
            job.JobName,
            claim.ScheduledAtUtc,
            startedAtUtc,
            completedAtUtc,
            status.ToString(),
            resultMessage,
            error);
    }

    private static CronJobExecutionRecordSummary MapExecutionRecord(Exchange.Infrastructure.Scheduling.Persistence.CronJobExecutionRecordEntity entity)
    {
        return new CronJobExecutionRecordSummary(
            entity.Id,
            entity.ScheduledAtUtc,
            entity.StartedAtUtc,
            entity.CompletedAtUtc,
            entity.Status.ToString(),
            entity.ResultMessage,
            entity.Error,
            entity.RunnerId);
    }
}
