export const GATEWAY_PROVIDER = {
  Kraken: 'kraken',
  Coinbase: 'coinbase'
} as const;

export type GatewayProvider = (typeof GATEWAY_PROVIDER)[keyof typeof GATEWAY_PROVIDER];

export interface CryptoGatewaySettingsProfile {
  readonly id: string;
  readonly name: string;
  readonly provider: string;
  readonly enabled: boolean;
  readonly baseUrl: string;
  readonly httpTimeoutSeconds: number;
  readonly providerSettingsJson: string;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface UpsertCryptoGatewaySettingsRequest {
  name: string;
  provider: GatewayProvider;
  enabled: boolean;
  baseUrl: string;
  httpTimeoutSeconds: number;
  providerSettingsJson: string;
}

export interface SaveCryptoGatewayCredentialsRequest {
  apiKey: string;
  apiSecret: string;
}

export const DEFAULT_CRYPTO_GATEWAY_SETTINGS_REQUEST: UpsertCryptoGatewaySettingsRequest = {
  name: 'Kraken default',
  provider: GATEWAY_PROVIDER.Kraken,
  enabled: false,
  baseUrl: 'https://api.kraken.com',
  httpTimeoutSeconds: 15,
  providerSettingsJson: '{\n  "bitcoinWithdrawalKey": "",\n  "etherWithdrawalKey": "",\n  "bitcoinRequiredConfirmations": 3,\n  "etherRequiredConfirmations": 12\n}'
};

export function toUpsertGatewayRequest(profile: CryptoGatewaySettingsProfile): UpsertCryptoGatewaySettingsRequest {
  return {
    name: profile.name,
    provider: profile.provider as GatewayProvider,
    enabled: profile.enabled,
    baseUrl: profile.baseUrl,
    httpTimeoutSeconds: profile.httpTimeoutSeconds,
    providerSettingsJson: profile.providerSettingsJson
  };
}
