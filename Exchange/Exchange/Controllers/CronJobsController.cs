using Exchange.CryptoTransactions.Application;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Controllers;

[ApiController]
[Route("api/admin/cron-jobs")]
public sealed class CronJobsController(ICronJobsAdminService cronJobsAdminService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CronJobAdminSummary>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var jobs = await cronJobsAdminService.GetAllAsync(cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("{jobName}/executions")]
    public async Task<ActionResult<IReadOnlyList<CronJobExecutionRecordSummary>>> GetExecutionsAsync(
        string jobName,
        CancellationToken cancellationToken)
    {
        var executions = await cronJobsAdminService.GetExecutionsAsync(jobName, cancellationToken);
        return executions is null ? NotFound() : Ok(executions);
    }

    [HttpPost("{jobName}/run")]
    public async Task<ActionResult<CronJobManualRunResult>> RunNowAsync(string jobName, CancellationToken cancellationToken)
    {
        try
        {
            var runResult = await cronJobsAdminService.RunNowAsync(jobName, cancellationToken);
            return runResult is null ? NotFound() : Ok(runResult);
        }
        catch (CronJobRunRejectedException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Cron job run rejected",
                Status = StatusCodes.Status409Conflict,
                Detail = exception.Message
            });
        }
    }
}
