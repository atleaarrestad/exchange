namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class ExternalHedgeExecutionQuantityMismatchException(string message) : Exception(message);
