namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IBlockchainTransferGateway
{
    Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default);
}
