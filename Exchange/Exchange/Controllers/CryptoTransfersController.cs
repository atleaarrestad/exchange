using Exchange.CryptoTransactions.Application;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Controllers;

[ApiController]
[Route("api/crypto-transfers")]
public sealed class CryptoTransfersController(ICryptoTransferService cryptoTransferService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CryptoTransferReceipt>> SubmitAsync(
        [FromBody] SubmitCryptoTransferRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new SubmitCryptoTransferCommand(
            request.IdempotencyKey,
            request.SourceAccountId,
            request.DestinationAddress,
            request.AssetSymbol,
            request.Amount,
            request.NetworkFee);

        var result = await cryptoTransferService.SubmitAsync(command, cancellationToken);
        return Ok(result);
    }
}

public sealed record SubmitCryptoTransferRequest(
    string IdempotencyKey,
    string SourceAccountId,
    string DestinationAddress,
    string AssetSymbol,
    decimal Amount,
    decimal NetworkFee);
