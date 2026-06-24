namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IExternalHedgeExecutionReadinessGate
{
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
}
