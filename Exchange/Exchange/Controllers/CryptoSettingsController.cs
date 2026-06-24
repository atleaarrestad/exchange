using Exchange.CryptoTransactions.Application;
using Exchange.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Controllers;

[ApiController]
[Route("api/admin/crypto-settings")]
public sealed class CryptoSettingsController(ICryptoSettingsService cryptoSettingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CryptoSettingsProfile>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var settings = await cryptoSettingsService.GetAllAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CryptoSettingsProfile>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await cryptoSettingsService.GetByIdAsync(id, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    [HttpPost]
    public async Task<ActionResult<CryptoSettingsProfile>> CreateAsync(
        [FromBody] UpsertCryptoSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var created = await cryptoSettingsService.CreateAsync(
            ToCreateCommand(request),
            cancellationToken);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CryptoSettingsProfile>> UpdateAsync(
        Guid id,
        [FromBody] UpsertCryptoSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await cryptoSettingsService.UpdateAsync(
            id,
            ToUpdateCommand(request),
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await cryptoSettingsService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static CreateCryptoSettingsProfileCommand ToCreateCommand(UpsertCryptoSettingsRequest request)
    {
        return new CreateCryptoSettingsProfileCommand(
            request.Name.Trim(),
            request.QuoteTtlSeconds,
            request.InternalOnlySpreadBasisPoints,
            request.ExternalHedgeSpreadBasisPoints,
            request.MaxAllowedSlippageBasisPoints,
            request.BitcoinReferencePriceNok,
            request.EtherReferencePriceNok,
            request.InitialBitcoinInventory,
            request.InitialEtherInventory,
            request.MaxBufferedHedgeCustomerBuys,
            request.MaxBufferedHedgeDelaySeconds,
            request.TimeoutReconciliationScanIntervalSeconds,
            request.TimeoutReconciliationStaleAfterSeconds,
            request.SimulationMinLatencyMs,
            request.SimulationMaxLatencyMs,
            request.SimulationRejectRate,
            request.SimulationTimeoutRate,
            request.SimulationDefaultBitcoinAvailableBalance,
            request.SimulationDefaultEtherAvailableBalance);
    }

    private static UpdateCryptoSettingsProfileCommand ToUpdateCommand(UpsertCryptoSettingsRequest request)
    {
        return new UpdateCryptoSettingsProfileCommand(
            request.Name.Trim(),
            request.QuoteTtlSeconds,
            request.InternalOnlySpreadBasisPoints,
            request.ExternalHedgeSpreadBasisPoints,
            request.MaxAllowedSlippageBasisPoints,
            request.BitcoinReferencePriceNok,
            request.EtherReferencePriceNok,
            request.InitialBitcoinInventory,
            request.InitialEtherInventory,
            request.MaxBufferedHedgeCustomerBuys,
            request.MaxBufferedHedgeDelaySeconds,
            request.TimeoutReconciliationScanIntervalSeconds,
            request.TimeoutReconciliationStaleAfterSeconds,
            request.SimulationMinLatencyMs,
            request.SimulationMaxLatencyMs,
            request.SimulationRejectRate,
            request.SimulationTimeoutRate,
            request.SimulationDefaultBitcoinAvailableBalance,
            request.SimulationDefaultEtherAvailableBalance);
    }
}
