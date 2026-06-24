export const OPERATING_ENVIRONMENT = {
  Local: 'Local',
  Simulation: 'Simulation',
  Staging: 'Staging'
} as const;

export type OperatingEnvironment = (typeof OPERATING_ENVIRONMENT)[keyof typeof OPERATING_ENVIRONMENT];

export const HEALTH_STATE = {
  Healthy: 'Healthy',
  Degraded: 'Degraded',
  Down: 'Down'
} as const;

export type HealthState = (typeof HEALTH_STATE)[keyof typeof HEALTH_STATE];

export const INVARIANT_STATE = {
  Passing: 'Passing',
  Warning: 'Warning',
  Failed: 'Failed'
} as const;

export type InvariantState = (typeof INVARIANT_STATE)[keyof typeof INVARIANT_STATE];

export const EVENT_SEVERITY = {
  Info: 'Info',
  Warning: 'Warning',
  Error: 'Error'
} as const;

export type EventSeverity = (typeof EVENT_SEVERITY)[keyof typeof EVENT_SEVERITY];

export interface OpsScenario {
  readonly id: string;
  readonly name: string;
  readonly description: string;
}

export interface ServiceHealthItem {
  readonly serviceName: string;
  readonly state: HealthState;
  readonly latencyMs: number;
  readonly queueDepth: number;
  readonly lastUpdatedIsoUtc: string;
}

export interface InvariantCheck {
  readonly name: string;
  readonly state: InvariantState;
  readonly detail: string;
}

export interface TimelineEvent {
  readonly occurredAtIsoUtc: string;
  readonly correlationId: string;
  readonly step: string;
  readonly severity: EventSeverity;
  readonly detail: string;
}

export interface OpsSnapshot {
  readonly operatingEnvironment: OperatingEnvironment;
  readonly modeLabel: string;
  readonly scenario: OpsScenario;
  readonly serviceHealth: readonly ServiceHealthItem[];
  readonly invariantChecks: readonly InvariantCheck[];
  readonly timelineEvents: readonly TimelineEvent[];
}

export interface StartBrokeredBuySimulationRequest {
  readonly customerAccountId: string;
  readonly assetSymbol: string;
  readonly quantity: number;
  readonly quoteCurrency: string;
  readonly customerFiatAvailableBalance: number;
  readonly platformBitcoinInventory: number;
  readonly platformEtherInventory: number;
  readonly maxUnitPrice: number | null;
  readonly maxTotalCost: number | null;
  readonly clientOrderId: string | null;
}

export interface StartBrokeredBuySimulationResponse {
  readonly correlationId: string;
  readonly quoteId: string;
  readonly clientOrderId: string;
  readonly customerAccountId: string;
  readonly assetSymbol: string;
  readonly quantity: number;
  readonly quoteCurrency: string;
  readonly seededCustomerFiatAvailableBalance: number;
  readonly seededPlatformBitcoinInventory: number;
  readonly seededPlatformEtherInventory: number;
  readonly startedAtUtc: string;
  readonly status: string;
}

export interface ResetBrokeredBuySimulationDataResponse {
  readonly resetAtUtc: string;
  readonly status: string;
}
