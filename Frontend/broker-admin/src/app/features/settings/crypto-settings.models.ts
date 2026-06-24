export interface CryptoSettingsProfile {
  readonly id: string;
  readonly name: string;
  readonly quoteTtlSeconds: number;
  readonly internalOnlySpreadBasisPoints: number;
  readonly externalHedgeSpreadBasisPoints: number;
  readonly maxAllowedSlippageBasisPoints: number;
  readonly bitcoinReferencePriceNok: number;
  readonly etherReferencePriceNok: number;
  readonly initialBitcoinInventory: number;
  readonly initialEtherInventory: number;
  readonly maxBufferedHedgeCustomerBuys: number;
  readonly maxBufferedHedgeDelaySeconds: number;
  readonly timeoutReconciliationScanIntervalSeconds: number;
  readonly timeoutReconciliationStaleAfterSeconds: number;
  readonly simulationMinLatencyMs: number;
  readonly simulationMaxLatencyMs: number;
  readonly simulationRejectRate: number;
  readonly simulationTimeoutRate: number;
  readonly simulationDefaultBitcoinAvailableBalance: number;
  readonly simulationDefaultEtherAvailableBalance: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface UpsertCryptoSettingsRequest {
  name: string;
  quoteTtlSeconds: number;
  internalOnlySpreadBasisPoints: number;
  externalHedgeSpreadBasisPoints: number;
  maxAllowedSlippageBasisPoints: number;
  bitcoinReferencePriceNok: number;
  etherReferencePriceNok: number;
  initialBitcoinInventory: number;
  initialEtherInventory: number;
  maxBufferedHedgeCustomerBuys: number;
  maxBufferedHedgeDelaySeconds: number;
  timeoutReconciliationScanIntervalSeconds: number;
  timeoutReconciliationStaleAfterSeconds: number;
  simulationMinLatencyMs: number;
  simulationMaxLatencyMs: number;
  simulationRejectRate: number;
  simulationTimeoutRate: number;
  simulationDefaultBitcoinAvailableBalance: number;
  simulationDefaultEtherAvailableBalance: number;
}

export const DEFAULT_CRYPTO_SETTINGS_REQUEST: UpsertCryptoSettingsRequest = {
  name: 'Default',
  quoteTtlSeconds: 15,
  internalOnlySpreadBasisPoints: 35,
  externalHedgeSpreadBasisPoints: 90,
  maxAllowedSlippageBasisPoints: 200,
  bitcoinReferencePriceNok: 1_000_000,
  etherReferencePriceNok: 50_000,
  initialBitcoinInventory: 2,
  initialEtherInventory: 25,
  maxBufferedHedgeCustomerBuys: 10,
  maxBufferedHedgeDelaySeconds: 30,
  timeoutReconciliationScanIntervalSeconds: 30,
  timeoutReconciliationStaleAfterSeconds: 45,
  simulationMinLatencyMs: 20,
  simulationMaxLatencyMs: 120,
  simulationRejectRate: 0.02,
  simulationTimeoutRate: 0.01,
  simulationDefaultBitcoinAvailableBalance: 2,
  simulationDefaultEtherAvailableBalance: 20
};

export function toUpsertRequest(profile: CryptoSettingsProfile): UpsertCryptoSettingsRequest {
  return {
    name: profile.name,
    quoteTtlSeconds: profile.quoteTtlSeconds,
    internalOnlySpreadBasisPoints: profile.internalOnlySpreadBasisPoints,
    externalHedgeSpreadBasisPoints: profile.externalHedgeSpreadBasisPoints,
    maxAllowedSlippageBasisPoints: profile.maxAllowedSlippageBasisPoints,
    bitcoinReferencePriceNok: profile.bitcoinReferencePriceNok,
    etherReferencePriceNok: profile.etherReferencePriceNok,
    initialBitcoinInventory: profile.initialBitcoinInventory,
    initialEtherInventory: profile.initialEtherInventory,
    maxBufferedHedgeCustomerBuys: profile.maxBufferedHedgeCustomerBuys,
    maxBufferedHedgeDelaySeconds: profile.maxBufferedHedgeDelaySeconds,
    timeoutReconciliationScanIntervalSeconds: profile.timeoutReconciliationScanIntervalSeconds,
    timeoutReconciliationStaleAfterSeconds: profile.timeoutReconciliationStaleAfterSeconds,
    simulationMinLatencyMs: profile.simulationMinLatencyMs,
    simulationMaxLatencyMs: profile.simulationMaxLatencyMs,
    simulationRejectRate: profile.simulationRejectRate,
    simulationTimeoutRate: profile.simulationTimeoutRate,
    simulationDefaultBitcoinAvailableBalance: profile.simulationDefaultBitcoinAvailableBalance,
    simulationDefaultEtherAvailableBalance: profile.simulationDefaultEtherAvailableBalance
  };
}
