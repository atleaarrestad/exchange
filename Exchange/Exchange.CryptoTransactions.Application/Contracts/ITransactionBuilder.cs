namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ITransactionBuilder
{
    BuiltTransaction Build(BlockchainTransferRequest request, decimal networkFee);
}
