using Exchange.FiatTransactions.Domain.ValueObjects;

namespace Exchange.FiatTransactions.Application.Contracts;

public sealed record FiatLedgerBrokeredBuyReservationCaptureCommand(
    string ClientOrderId,
    string CustomerAccountId,
    FiatCurrency FiatCurrency,
    decimal CustomerDebitAmount,
    DateTimeOffset ExecutedAtUtc);
