namespace Exchange.FiatTransactions.Application;

public sealed record FiatBankSettlementReceipt(
    string BankReferenceId,
    string FiatCurrency,
    decimal Amount,
    DateTimeOffset ExecutedAtUtc);
