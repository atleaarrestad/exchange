namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IFeeEstimator
{
    decimal NormalizeFee(BlockchainTransferRequest request);
}
