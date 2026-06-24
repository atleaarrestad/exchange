using Exchange.FiatTransactions.Domain.ValueObjects;

namespace Exchange.FiatTransactions.Application.Contracts;

public sealed record FiatLedgerBankSettlementPostingCommand(
    string BankReferenceId,
    FiatCurrency FiatCurrency,
    decimal Amount,
    DateTimeOffset ExecutedAtUtc);
