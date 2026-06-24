using Exchange.Contracts;
using Exchange.CryptoTransactions.Application;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Controllers;

[ApiController]
[Route("api/admin/crypto-gateway-settings")]
public sealed class CryptoGatewaySettingsController(ICryptoGatewaySettingsService gatewaySettingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CryptoGatewaySettingsProfile>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var profiles = await gatewaySettingsService.GetAllAsync(cancellationToken);
        return Ok(profiles);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CryptoGatewaySettingsProfile>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await gatewaySettingsService.GetByIdAsync(id, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    public async Task<ActionResult<CryptoGatewaySettingsProfile>> CreateAsync(
        [FromBody] UpsertCryptoGatewaySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var created = await gatewaySettingsService.CreateAsync(ToCreateCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CryptoGatewaySettingsProfile>> UpdateAsync(
        Guid id,
        [FromBody] UpsertCryptoGatewaySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await gatewaySettingsService.UpdateAsync(id, ToUpdateCommand(request), cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{id:guid}/credentials")]
    public async Task<IActionResult> SaveCredentialsAsync(
        Guid id,
        [FromBody] SaveCryptoGatewayCredentialsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await gatewaySettingsService.SaveCredentialsAsync(
            id,
            new SaveCryptoGatewayCredentialsCommand(
                request.ApiKey,
                request.ApiSecret),
            cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await gatewaySettingsService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static CreateCryptoGatewaySettingsProfileCommand ToCreateCommand(UpsertCryptoGatewaySettingsRequest request)
    {
        return new CreateCryptoGatewaySettingsProfileCommand(
            request.Name.Trim(),
            request.Provider.Trim(),
            request.Enabled,
            request.BaseUrl.Trim(),
            request.HttpTimeoutSeconds,
            request.ProviderSettingsJson.Trim());
    }

    private static UpdateCryptoGatewaySettingsProfileCommand ToUpdateCommand(UpsertCryptoGatewaySettingsRequest request)
    {
        return new UpdateCryptoGatewaySettingsProfileCommand(
            request.Name.Trim(),
            request.Provider.Trim(),
            request.Enabled,
            request.BaseUrl.Trim(),
            request.HttpTimeoutSeconds,
            request.ProviderSettingsJson.Trim());
    }
}
