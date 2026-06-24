namespace Exchange.FiatTransactions.Application;

public sealed record FiatBrokeredBuySettlementReceipt(
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal CustomerDebitAmount,
    DateTimeOffset ExecutedAtUtc);
