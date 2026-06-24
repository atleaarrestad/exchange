namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class QuoteExecutionRejectedException(string message) : Exception(message);
