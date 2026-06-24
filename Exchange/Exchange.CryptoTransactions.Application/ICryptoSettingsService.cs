namespace Exchange.CryptoTransactions.Application;

public interface ICryptoSettingsService
{
    Task<IReadOnlyList<CryptoSettingsProfile>> GetAllAsync(CancellationToken cancellationToken);

    Task<CryptoSettingsProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CryptoSettingsProfile> CreateAsync(CreateCryptoSettingsProfileCommand command, CancellationToken cancellationToken);

    Task<CryptoSettingsProfile?> UpdateAsync(Guid id, UpdateCryptoSettingsProfileCommand command, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record CreateCryptoSettingsProfileCommand(
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

public sealed record UpdateCryptoSettingsProfileCommand(
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
