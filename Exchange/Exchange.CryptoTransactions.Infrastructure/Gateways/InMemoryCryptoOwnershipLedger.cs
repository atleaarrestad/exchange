using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class InMemoryCryptoOwnershipLedger(BrokeredTradingOptions options) : ICryptoOwnershipLedger
{
    private readonly Lock gate = new();
    private readonly Dictionary<AssetSymbol, decimal> platformInventory = new()
    {
        [AssetSymbol.Bitcoin] = options.GetInitialInventory(AssetSymbol.Bitcoin),
        [AssetSymbol.Ether] = options.GetInitialInventory(AssetSymbol.Ether)
    };
    private readonly Dictionary<(string CustomerAccountId, AssetSymbol AssetSymbol), decimal> customerHoldings = new();
    private readonly Dictionary<(string CustomerAccountId, AssetSymbol AssetSymbol, string ClientOrderId), BrokeredCryptoBuyReceipt> executedBuys = new();

    public Task<decimal> GetAvailablePlatformInventoryAsync(AssetSymbol assetSymbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!platformInventory.TryGetValue(assetSymbol, out var quantity))
            {
                quantity = 0m;
                platformInventory[assetSymbol] = quantity;
            }

            return Task.FromResult(quantity);
        }
    }

    public Task<BrokeredCryptoBuyReceipt?> GetRecordedCustomerBuyAsync(
        string customerAccountId,
        AssetSymbol assetSymbol,
        string clientOrderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderId);
        cancellationToken.ThrowIfCancellationRequested();

        var key = (customerAccountId.Trim(), assetSymbol, clientOrderId.Trim());
        lock (gate)
        {
            executedBuys.TryGetValue(key, out var existing);
            return Task.FromResult(existing);
        }
    }

    public Task<BrokeredCryptoBuyReceipt> RecordCustomerBuyAsync(
        OwnershipLedgerBuyRecordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var customerAccountId = command.CustomerAccountId.Trim();
        var clientOrderId = command.ClientOrderId.Trim();
        var executionKey = (customerAccountId, command.AssetSymbol, clientOrderId);

        lock (gate)
        {
            if (executedBuys.TryGetValue(executionKey, out var existing))
            {
                if (!Matches(command, existing))
                {
                    throw new IdempotencyKeyConflictException(
                        $"Client order id '{clientOrderId}' was already used with a different brokered buy request.");
                }

                return Task.FromResult(existing);
            }

            if (!platformInventory.TryGetValue(command.AssetSymbol, out var currentInventory))
            {
                currentInventory = 0m;
            }

            if (command.InternalFillQuantity > currentInventory)
            {
                throw new InsufficientFundsException(
                    $"Insufficient internal inventory for {command.AssetSymbol.Value}. Available: {currentInventory}, required: {command.InternalFillQuantity}.");
            }

            platformInventory[command.AssetSymbol] = checked(currentInventory - command.InternalFillQuantity);
            var holdingsKey = (customerAccountId, command.AssetSymbol);
            customerHoldings.TryGetValue(holdingsKey, out var currentHolding);
            customerHoldings[holdingsKey] = checked(currentHolding + command.Quantity);

            var receipt = new BrokeredCryptoBuyReceipt(
                clientOrderId,
                customerAccountId,
                command.AssetSymbol.Value,
                command.QuoteCurrency.Value,
                command.Quantity,
                command.InternalFillQuantity,
                command.ExternalHedgeQuantity,
                command.UnitPrice,
                command.TotalCost,
                command.ExecutedAtUtc,
                command.ExternalHedgeOrderId);
            executedBuys[executionKey] = receipt;
            return Task.FromResult(receipt);
        }
    }

    private static bool Matches(OwnershipLedgerBuyRecordCommand command, BrokeredCryptoBuyReceipt existing)
    {
        return existing.Quantity == command.Quantity
            && existing.InternalFillQuantity == command.InternalFillQuantity
            && existing.ExternalHedgeQuantity == command.ExternalHedgeQuantity
            && existing.UnitPrice == command.UnitPrice
            && existing.TotalCost == command.TotalCost
            && string.Equals(existing.QuoteCurrency, command.QuoteCurrency.Value, StringComparison.Ordinal)
            && string.Equals(existing.AssetSymbol, command.AssetSymbol.Value, StringComparison.Ordinal)
            && string.Equals(existing.ExternalHedgeOrderId, command.ExternalHedgeOrderId, StringComparison.Ordinal);
    }
}
