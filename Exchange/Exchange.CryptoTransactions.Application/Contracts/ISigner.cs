namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ISigner
{
    SignedTransaction Sign(BuiltTransaction transaction);
}
