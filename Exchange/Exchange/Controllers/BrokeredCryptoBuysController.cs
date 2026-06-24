using Exchange.CryptoTransactions.Application;
using Exchange.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Controllers;

[ApiController]
[Route("api/brokered-crypto-buys")]
public sealed class BrokeredCryptoBuysController(IBrokeredCryptoBuyService brokeredCryptoBuyService) : ControllerBase
{
    [HttpPost("quote")]
    public async Task<ActionResult<BrokeredCryptoBuyQuote>> QuoteAsync(
        [FromBody] QuoteBrokeredCryptoBuyRequest request,
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

        var quote = await brokeredCryptoBuyService.QuoteAsync(
            new QuoteBrokeredCryptoBuyCommand(
                request.CustomerAccountId,
                request.AssetSymbol,
                request.Quantity,
                request.QuoteCurrency),
            cancellationToken);

        return Ok(quote);
    }

    [HttpPost]
    public async Task<ActionResult<BrokeredCryptoBuyReceipt>> ExecuteAsync(
        [FromBody] ExecuteBrokeredCryptoBuyRequest request,
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

        var receipt = await brokeredCryptoBuyService.ExecuteAsync(
            new ExecuteBrokeredCryptoBuyCommand(
                request.QuoteId,
                request.ClientOrderId,
                request.CustomerAccountId,
                request.AssetSymbol,
                request.Quantity,
                request.QuoteCurrency,
                request.MaxUnitPrice,
                request.MaxTotalCost),
            cancellationToken);

        return Ok(receipt);
    }
}
