using Exchange.FiatTransactions.Domain.ValueObjects;

namespace Exchange.FiatTransactions.Application.Contracts;

public interface IFiatLedger
{
    Task<decimal> GetCustomerAvailableBalanceAsync(
        string customerAccountId,
        FiatCurrency fiatCurrency,
        CancellationToken cancellationToken = default);

    Task<decimal> GetPlatformTradeClearingBalanceAsync(
        FiatCurrency fiatCurrency,
        CancellationToken cancellationToken = default);

    Task<FiatBrokeredBuySettlementReceipt?> GetRecordedBrokeredBuySettlementAsync(
        string customerAccountId,
        string clientOrderId,
        CancellationToken cancellationToken = default);

    Task<FiatBrokeredBuySettlementReceipt> RecordBrokeredBuySettlementAsync(
        FiatLedgerBrokeredBuyPostingCommand command,
        CancellationToken cancellationToken = default);

    Task<FiatBankSettlementReceipt?> GetRecordedBankSettlementAsync(
        string bankReferenceId,
        CancellationToken cancellationToken = default);

    Task<FiatBankSettlementReceipt> RecordBankSettlementAsync(
        FiatLedgerBankSettlementPostingCommand command,
        CancellationToken cancellationToken = default);
}
