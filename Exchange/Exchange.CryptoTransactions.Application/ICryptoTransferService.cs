namespace Exchange.CryptoTransactions.Application;

public interface ICryptoTransferService
{
    Task<CryptoTransferReceipt> SubmitAsync(SubmitCryptoTransferCommand command, CancellationToken cancellationToken = default);
}
