using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ICryptoOwnershipLedger
{
    Task<decimal> GetAvailablePlatformInventoryAsync(AssetSymbol assetSymbol, CancellationToken cancellationToken = default);
    Task<BrokeredCryptoBuyReceipt?> GetRecordedCustomerBuyAsync(
        string customerAccountId,
        AssetSymbol assetSymbol,
        string clientOrderId,
        CancellationToken cancellationToken = default);
    Task<BrokeredCryptoBuyReceipt> RecordCustomerBuyAsync(
        OwnershipLedgerBuyRecordCommand command,
        CancellationToken cancellationToken = default);
}
