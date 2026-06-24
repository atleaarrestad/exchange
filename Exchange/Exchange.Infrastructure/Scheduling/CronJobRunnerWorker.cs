using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exchange.Infrastructure.Scheduling;

public sealed class CronJobRunnerWorker(
    ICronJobStateStore stateStore,
    IEnumerable<ICronScheduledJob> jobs,
    CronJobRunnerOptions options,
    TimeProvider timeProvider,
    ILogger<CronJobRunnerWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName.ToLowerInvariant()}-{Environment.ProcessId}-{Guid.CreateVersion7()}";
    private readonly IReadOnlyList<ICronScheduledJob> enabledJobs = jobs.Where(job => job.Enabled).ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (enabledJobs.Count == 0)
        {
            logger.LogInformation("Cron job runner started with no enabled jobs.");
            return;
        }

        using var timer = new PeriodicTimer(options.PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var job in enabledJobs)
            {
                stoppingToken.ThrowIfCancellationRequested();
                try
                {
                    await TryRunJobAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Cron job runner iteration failed for {JobName}.", job.JobName);
                }
            }
        }
    }

    private async Task TryRunJobAsync(ICronScheduledJob job, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await stateStore.EnsureStateAsync(job.JobName, job.CronExpression, job.Enabled, now, cancellationToken);

        var minimumLeaseDuration = job.Timeout + TimeSpan.FromSeconds(30);
        var leaseDuration = options.LeaseDuration >= minimumLeaseDuration ? options.LeaseDuration : minimumLeaseDuration;
        var claim = await stateStore.TryClaimAsync(job.JobName, workerId, now, leaseDuration, cancellationToken);
        if (claim is null)
        {
            return;
        }

        var startedAtUtc = timeProvider.GetUtcNow();
        var runRecordId = await stateStore.StartExecutionAsync(
            job.JobName,
            workerId,
            claim.ScheduledAtUtc,
            startedAtUtc,
            cancellationToken);

        var completedAtUtc = timeProvider.GetUtcNow();
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
    }
}
