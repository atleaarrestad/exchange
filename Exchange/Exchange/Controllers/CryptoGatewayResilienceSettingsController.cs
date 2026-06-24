using Exchange.Contracts;
using Exchange.CryptoTransactions.Application;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Controllers;

[ApiController]
[Route("api/admin/crypto-gateway-resilience-settings")]
public sealed class CryptoGatewayResilienceSettingsController(
    ICryptoGatewayResilienceSettingsService resilienceSettingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CryptoGatewayResilienceSettingsProfile>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var profiles = await resilienceSettingsService.GetAllAsync(cancellationToken);
        return Ok(profiles);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CryptoGatewayResilienceSettingsProfile>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await resilienceSettingsService.GetByIdAsync(id, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    public async Task<ActionResult<CryptoGatewayResilienceSettingsProfile>> CreateAsync(
        [FromBody] UpsertCryptoGatewayResilienceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var created = await resilienceSettingsService.CreateAsync(ToCreateCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CryptoGatewayResilienceSettingsProfile>> UpdateAsync(
        Guid id,
        [FromBody] UpsertCryptoGatewayResilienceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await resilienceSettingsService.UpdateAsync(id, ToUpdateCommand(request), cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await resilienceSettingsService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static CreateCryptoGatewayResilienceSettingsProfileCommand ToCreateCommand(UpsertCryptoGatewayResilienceSettingsRequest request)
    {
        return new CreateCryptoGatewayResilienceSettingsProfileCommand(
            request.Name.Trim(),
            request.Enabled,
            request.OperationTimeoutSeconds,
            request.RetryCount,
            request.RetryDelayMilliseconds,
            request.FailureRatio,
            request.MinimumThroughput,
            request.SamplingDurationSeconds,
            request.BreakDurationSeconds,
            request.MaxParallelization,
            request.MaxQueueingActions);
    }

    private static UpdateCryptoGatewayResilienceSettingsProfileCommand ToUpdateCommand(UpsertCryptoGatewayResilienceSettingsRequest request)
    {
        return new UpdateCryptoGatewayResilienceSettingsProfileCommand(
            request.Name.Trim(),
            request.Enabled,
            request.OperationTimeoutSeconds,
            request.RetryCount,
            request.RetryDelayMilliseconds,
            request.FailureRatio,
            request.MinimumThroughput,
            request.SamplingDurationSeconds,
            request.BreakDurationSeconds,
            request.MaxParallelization,
            request.MaxQueueingActions);
    }
}
