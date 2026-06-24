namespace Exchange.Contracts;

public sealed record UpsertCryptoSettingsRequest(
    string Name,
    int QuoteTtlSeconds,
    decimal InternalOnlySpreadBasisPoints,
    decimal ExternalHedgeSpreadBasisPoints,
    decimal MaxAllowedSlippageBasisPoints,
    decimal BitcoinReferencePriceNok,
    decimal EtherReferencePriceNok,
    decimal InitialBitcoinInventory,
    decimal InitialEtherInventory,
    int MaxBufferedHedgeCustomerBuys,
    int MaxBufferedHedgeDelaySeconds,
    int TimeoutReconciliationScanIntervalSeconds,
    int TimeoutReconciliationStaleAfterSeconds,
    int SimulationMinLatencyMs,
    int SimulationMaxLatencyMs,
    decimal SimulationRejectRate,
    decimal SimulationTimeoutRate,
    decimal SimulationDefaultBitcoinAvailableBalance,
    decimal SimulationDefaultEtherAvailableBalance);
