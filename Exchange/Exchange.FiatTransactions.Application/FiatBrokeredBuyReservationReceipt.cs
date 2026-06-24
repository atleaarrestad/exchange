namespace Exchange.FiatTransactions.Application;

public sealed record FiatBrokeredBuyReservationReceipt(
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal ReservedAmount,
    DateTimeOffset ReservedAtUtc);
