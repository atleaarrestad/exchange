namespace Exchange.FiatTransactions.Application;

public sealed record FiatBrokeredBuyReservationReleaseReceipt(
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal ReleasedAmount,
    DateTimeOffset ReleasedAtUtc);
