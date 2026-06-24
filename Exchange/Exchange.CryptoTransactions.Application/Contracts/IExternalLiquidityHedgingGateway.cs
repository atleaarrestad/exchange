namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IExternalLiquidityHedgingGateway
{
    Task<HedgePurchaseResult> BuyAsync(HedgePurchaseRequest request, CancellationToken cancellationToken = default);
}
