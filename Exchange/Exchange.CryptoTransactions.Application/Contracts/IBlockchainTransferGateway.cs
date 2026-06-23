using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IBlockchainTransferGateway
{
    Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default);

    Task<BlockchainTransferStatus> GetTransferStatusAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
