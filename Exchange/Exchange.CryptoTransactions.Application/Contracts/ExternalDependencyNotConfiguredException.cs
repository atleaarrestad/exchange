namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class ExternalDependencyNotConfiguredException : Exception
{
    public ExternalDependencyNotConfiguredException(string message)
        : base(message)
    {
    }
}
