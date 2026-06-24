namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record PriceQuote(
    decimal UnitPrice,
    DateTimeOffset AsOfUtc,
    string Source);
