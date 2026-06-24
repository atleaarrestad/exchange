using Exchange.FiatTransactions.Domain.ValueObjects;

namespace Exchange.FiatTransactions.Application.Contracts;

public sealed record FiatLedgerBrokeredBuyReservationCommand(
    string ClientOrderId,
    string CustomerAccountId,
    FiatCurrency FiatCurrency,
    decimal ReservedAmount,
    DateTimeOffset ReservedAtUtc);
