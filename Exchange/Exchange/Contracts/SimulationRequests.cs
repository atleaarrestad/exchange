namespace Exchange.Contracts;

public sealed record StartBrokeredBuySimulationRequest(
    string CustomerAccountId,
    string AssetSymbol = "BTC",
    decimal Quantity = 0.10m,
    string QuoteCurrency = "NOK",
    decimal CustomerFiatAvailableBalance = 500_000m,
    decimal PlatformBitcoinInventory = 2m,
    decimal PlatformEtherInventory = 20m,
    decimal? MaxUnitPrice = null,
    decimal? MaxTotalCost = null,
    string? ClientOrderId = null);

public sealed record StartBrokeredBuySimulationResponse(
    Guid CorrelationId,
    Guid QuoteId,
    string ClientOrderId,
    string CustomerAccountId,
    string AssetSymbol,
    decimal Quantity,
    string QuoteCurrency,
    decimal SeededCustomerFiatAvailableBalance,
    decimal SeededPlatformBitcoinInventory,
    decimal SeededPlatformEtherInventory,
    DateTimeOffset StartedAtUtc,
    string Status);

public sealed record ResetBrokeredBuySimulationDataResponse(
    DateTimeOffset ResetAtUtc,
    string Status);
