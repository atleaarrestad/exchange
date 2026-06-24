using Exchange.FiatTransactions.Domain.ValueObjects;

namespace Exchange.FiatTransactions.Application.Contracts;

public sealed record FiatLedgerBrokeredBuyReservationReleaseCommand(
    string ClientOrderId,
    string CustomerAccountId,
    FiatCurrency FiatCurrency,
    decimal ReleasedAmount,
    DateTimeOffset ReleasedAtUtc);
