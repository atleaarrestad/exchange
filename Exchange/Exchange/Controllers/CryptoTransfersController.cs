using Exchange.CryptoTransactions.Application;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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
    [property: Required(AllowEmptyStrings = false)]
    [property: StringLength(128)]
    string IdempotencyKey,
    [property: Required(AllowEmptyStrings = false)]
    [property: StringLength(64, MinimumLength = 3)]
    [property: RegularExpression("^[A-Za-z0-9_-]+$")]
    string SourceAccountId,
    [property: Required(AllowEmptyStrings = false)]
    [property: StringLength(128, MinimumLength = 16)]
    string DestinationAddress,
    [property: Required(AllowEmptyStrings = false)]
    [property: StringLength(10, MinimumLength = 2)]
    [property: RegularExpression("^[A-Z]+$")]
    string AssetSymbol,
    [property: Range(typeof(decimal), "0.000000000000000001", "1000000")]
    decimal Amount,
    [property: Range(typeof(decimal), "0", "10")]
    decimal NetworkFee);
