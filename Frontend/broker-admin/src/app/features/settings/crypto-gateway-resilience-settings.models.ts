export interface CryptoGatewayResilienceSettingsProfile {
  readonly id: string;
  readonly name: string;
  readonly enabled: boolean;
  readonly operationTimeoutSeconds: number;
  readonly retryCount: number;
  readonly retryDelayMilliseconds: number;
  readonly failureRatio: number;
  readonly minimumThroughput: number;
  readonly samplingDurationSeconds: number;
  readonly breakDurationSeconds: number;
  readonly maxParallelization: number;
  readonly maxQueueingActions: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface UpsertCryptoGatewayResilienceSettingsRequest {
  name: string;
  enabled: boolean;
  operationTimeoutSeconds: number;
  retryCount: number;
  retryDelayMilliseconds: number;
  failureRatio: number;
  minimumThroughput: number;
  samplingDurationSeconds: number;
  breakDurationSeconds: number;
  maxParallelization: number;
  maxQueueingActions: number;
}

export const DEFAULT_CRYPTO_GATEWAY_RESILIENCE_SETTINGS_REQUEST: UpsertCryptoGatewayResilienceSettingsRequest = {
  name: 'Default resilience policy',
  enabled: true,
  operationTimeoutSeconds: 20,
  retryCount: 0,
  retryDelayMilliseconds: 2000,
  failureRatio: 0.5,
  minimumThroughput: 20,
  samplingDurationSeconds: 30,
  breakDurationSeconds: 30,
  maxParallelization: 32,
  maxQueueingActions: 64
};

export function toUpsertResilienceRequest(
  profile: CryptoGatewayResilienceSettingsProfile
): UpsertCryptoGatewayResilienceSettingsRequest {
  return {
    name: profile.name,
    enabled: profile.enabled,
    operationTimeoutSeconds: profile.operationTimeoutSeconds,
    retryCount: profile.retryCount,
    retryDelayMilliseconds: profile.retryDelayMilliseconds,
    failureRatio: profile.failureRatio,
    minimumThroughput: profile.minimumThroughput,
    samplingDurationSeconds: profile.samplingDurationSeconds,
    breakDurationSeconds: profile.breakDurationSeconds,
    maxParallelization: profile.maxParallelization,
    maxQueueingActions: profile.maxQueueingActions
  };
}
