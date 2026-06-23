namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IBlockchainTransferStrategy
{
    string AssetSymbol { get; }

    Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default);
}
