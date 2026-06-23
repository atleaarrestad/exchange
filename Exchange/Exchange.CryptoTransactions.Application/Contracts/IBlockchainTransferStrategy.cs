using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IBlockchainTransferStrategy
{
    AssetSymbol AssetSymbol { get; }

    Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default);
}
