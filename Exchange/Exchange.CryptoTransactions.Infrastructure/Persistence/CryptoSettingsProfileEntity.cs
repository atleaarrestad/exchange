namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoSettingsProfileEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int QuoteTtlSeconds { get; set; }
    public decimal InternalOnlySpreadBasisPoints { get; set; }
    public decimal ExternalHedgeSpreadBasisPoints { get; set; }
    public decimal MaxAllowedSlippageBasisPoints { get; set; }
    public decimal BitcoinReferencePriceNok { get; set; }
    public decimal EtherReferencePriceNok { get; set; }
    public decimal InitialBitcoinInventory { get; set; }
    public decimal InitialEtherInventory { get; set; }
    public int MaxBufferedHedgeCustomerBuys { get; set; }
    public int MaxBufferedHedgeDelaySeconds { get; set; }

    public int TimeoutReconciliationScanIntervalSeconds { get; set; }
    public int TimeoutReconciliationStaleAfterSeconds { get; set; }

    public int SimulationMinLatencyMs { get; set; }
    public int SimulationMaxLatencyMs { get; set; }
    public decimal SimulationRejectRate { get; set; }
    public decimal SimulationTimeoutRate { get; set; }
    public decimal SimulationDefaultBitcoinAvailableBalance { get; set; }
    public decimal SimulationDefaultEtherAvailableBalance { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
