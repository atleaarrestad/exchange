namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IConfirmationPolicy
{
    int RequiredConfirmations { get; }
}
