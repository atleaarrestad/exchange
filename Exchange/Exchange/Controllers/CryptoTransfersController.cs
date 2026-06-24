using Exchange.CryptoTransactions.Application;
using Exchange.Contracts;
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
        if (request is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request",
                Detail = "Request body is required.",
                Type = "https://httpstatuses.com/400"
            });
        }

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
