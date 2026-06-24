namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class ExternalHedgeExecutionUnavailableException(string message) : Exception(message);
