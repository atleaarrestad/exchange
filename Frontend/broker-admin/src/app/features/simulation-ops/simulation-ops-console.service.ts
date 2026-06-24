import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AdminApiClientService } from '../../core/api/admin-api-client.service';
import {
  EVENT_SEVERITY,
  HEALTH_STATE,
  INVARIANT_STATE,
  OPERATING_ENVIRONMENT,
  OpsScenario,
  OpsSnapshot,
  ResetBrokeredBuySimulationDataResponse,
  StartBrokeredBuySimulationRequest,
  StartBrokeredBuySimulationResponse
} from './simulation-ops.models';

const BASELINE_SCENARIO: OpsScenario = {
  id: 'baseline',
  name: 'Baseline Load',
  description: 'Normal traffic with stable dependencies.'
};

const LATENCY_SPIKE_SCENARIO: OpsScenario = {
  id: 'latency-spike',
  name: 'Latency Spike',
  description: 'Simulated external dependency slowdown.'
};

const LIQUIDITY_DROP_SCENARIO: OpsScenario = {
  id: 'liquidity-drop',
  name: 'Liquidity Drop',
  description: 'Reduced available liquidity and delayed hedging.'
};

const START_BROKERED_BUY_SIMULATION_ENDPOINT = '/api/admin/simulation/brokered-buy/start';
const RESET_BROKERED_BUY_SIMULATION_DATA_ENDPOINT = '/api/admin/simulation/brokered-buy/reset';

const SNAPSHOTS: readonly OpsSnapshot[] = [
  {
    operatingEnvironment: OPERATING_ENVIRONMENT.Simulation,
    modeLabel: 'Simulation mode',
    scenario: BASELINE_SCENARIO,
    serviceHealth: [
      {
        serviceName: 'Quote Engine',
        state: HEALTH_STATE.Healthy,
        latencyMs: 18,
        queueDepth: 1,
        lastUpdatedIsoUtc: '2026-06-23T17:36:00Z'
      },
      {
        serviceName: 'Order Execution',
        state: HEALTH_STATE.Healthy,
        latencyMs: 24,
        queueDepth: 3,
        lastUpdatedIsoUtc: '2026-06-23T17:36:00Z'
      },
      {
        serviceName: 'Ledger Writer',
        state: HEALTH_STATE.Healthy,
        latencyMs: 14,
        queueDepth: 0,
        lastUpdatedIsoUtc: '2026-06-23T17:36:00Z'
      }
    ],
    invariantChecks: [
      {
        name: 'Negative balances',
        state: INVARIANT_STATE.Passing,
        detail: 'No accounts below zero.'
      },
      {
        name: 'Reserve threshold',
        state: INVARIANT_STATE.Passing,
        detail: 'Reserves at 122.4% of policy minimum.'
      },
      {
        name: 'Ledger reconciliation',
        state: INVARIANT_STATE.Passing,
        detail: 'No drift detected in last reconciliation window.'
      }
    ],
    timelineEvents: [
      {
        occurredAtIsoUtc: '2026-06-23T17:35:02Z',
        correlationId: 'ops-91ca4',
        step: 'Quote generated',
        severity: EVENT_SEVERITY.Info,
        detail: 'BTC-NOK quote published in 12 ms.'
      },
      {
        occurredAtIsoUtc: '2026-06-23T17:35:04Z',
        correlationId: 'ops-91ca4',
        step: 'Order executed',
        severity: EVENT_SEVERITY.Info,
        detail: 'Customer order executed against internal inventory.'
      },
      {
        occurredAtIsoUtc: '2026-06-23T17:35:06Z',
        correlationId: 'ops-91ca4',
        step: 'Ledger settled',
        severity: EVENT_SEVERITY.Info,
        detail: 'Double-entry posting completed with idempotency key.'
      }
    ]
  },
  {
    operatingEnvironment: OPERATING_ENVIRONMENT.Simulation,
    modeLabel: 'Simulation mode',
    scenario: LATENCY_SPIKE_SCENARIO,
    serviceHealth: [
      {
        serviceName: 'Quote Engine',
        state: HEALTH_STATE.Degraded,
        latencyMs: 420,
        queueDepth: 31,
        lastUpdatedIsoUtc: '2026-06-23T17:41:00Z'
      },
      {
        serviceName: 'Order Execution',
        state: HEALTH_STATE.Degraded,
        latencyMs: 280,
        queueDepth: 19,
        lastUpdatedIsoUtc: '2026-06-23T17:41:00Z'
      },
      {
        serviceName: 'Ledger Writer',
        state: HEALTH_STATE.Healthy,
        latencyMs: 21,
        queueDepth: 4,
        lastUpdatedIsoUtc: '2026-06-23T17:41:00Z'
      }
    ],
    invariantChecks: [
      {
        name: 'Negative balances',
        state: INVARIANT_STATE.Passing,
        detail: 'No accounts below zero.'
      },
      {
        name: 'Reserve threshold',
        state: INVARIANT_STATE.Warning,
        detail: 'Reserve buffer dropped to 103.1%; monitor hedging backlog.'
      },
      {
        name: 'Ledger reconciliation',
        state: INVARIANT_STATE.Passing,
        detail: 'Reconciliation still consistent.'
      }
    ],
    timelineEvents: [
      {
        occurredAtIsoUtc: '2026-06-23T17:40:12Z',
        correlationId: 'ops-9d17f',
        step: 'Quote refresh delayed',
        severity: EVENT_SEVERITY.Warning,
        detail: 'Market feed round-trip exceeded 400 ms.'
      },
      {
        occurredAtIsoUtc: '2026-06-23T17:40:18Z',
        correlationId: 'ops-9d17f',
        step: 'Execution retried',
        severity: EVENT_SEVERITY.Warning,
        detail: 'Retry succeeded with same idempotency key.'
      },
      {
        occurredAtIsoUtc: '2026-06-23T17:40:24Z',
        correlationId: 'ops-9d17f',
        step: 'Settlement completed',
        severity: EVENT_SEVERITY.Info,
        detail: 'Settlement completed with elevated latency.'
      }
    ]
  },
  {
    operatingEnvironment: OPERATING_ENVIRONMENT.Simulation,
    modeLabel: 'Simulation mode',
    scenario: LIQUIDITY_DROP_SCENARIO,
    serviceHealth: [
      {
        serviceName: 'Quote Engine',
        state: HEALTH_STATE.Degraded,
        latencyMs: 110,
        queueDepth: 15,
        lastUpdatedIsoUtc: '2026-06-23T17:46:00Z'
      },
      {
        serviceName: 'Order Execution',
        state: HEALTH_STATE.Down,
        latencyMs: 0,
        queueDepth: 48,
        lastUpdatedIsoUtc: '2026-06-23T17:46:00Z'
      },
      {
        serviceName: 'Ledger Writer',
        state: HEALTH_STATE.Healthy,
        latencyMs: 27,
        queueDepth: 6,
        lastUpdatedIsoUtc: '2026-06-23T17:46:00Z'
      }
    ],
    invariantChecks: [
      {
        name: 'Negative balances',
        state: INVARIANT_STATE.Passing,
        detail: 'No accounts below zero.'
      },
      {
        name: 'Reserve threshold',
        state: INVARIANT_STATE.Failed,
        detail: 'Reserve threshold breached at 96.4%.'
      },
      {
        name: 'Ledger reconciliation',
        state: INVARIANT_STATE.Warning,
        detail: 'Two unsettled postings waiting for liquidity recovery.'
      }
    ],
    timelineEvents: [
      {
        occurredAtIsoUtc: '2026-06-23T17:45:05Z',
        correlationId: 'ops-cf220',
        step: 'Liquidity reduction detected',
        severity: EVENT_SEVERITY.Warning,
        detail: 'Available hedge venue depth dropped below threshold.'
      },
      {
        occurredAtIsoUtc: '2026-06-23T17:45:17Z',
        correlationId: 'ops-cf220',
        step: 'Execution rejected',
        severity: EVENT_SEVERITY.Error,
        detail: 'Order rejected due to temporary liquidity protection.'
      },
      {
        occurredAtIsoUtc: '2026-06-23T17:45:31Z',
        correlationId: 'ops-cf220',
        step: 'Risk controls escalated',
        severity: EVENT_SEVERITY.Error,
        detail: 'Auto-throttle enabled and incident raised for on-call.'
      }
    ]
  }
];

@Injectable({ providedIn: 'root' })
export class SimulationOpsConsoleService {
  private readonly apiClient = inject(AdminApiClientService);
  private readonly snapshotIndex = signal(0);

  readonly snapshot = computed(() => SNAPSHOTS[this.snapshotIndex()]);
  readonly scenarios = computed(() => SNAPSHOTS.map((snapshot) => snapshot.scenario));

  readonly healthSummary = computed(() => {
    const serviceHealth = this.snapshot().serviceHealth;
    return {
      healthy: serviceHealth.filter((service) => service.state === HEALTH_STATE.Healthy).length,
      degraded: serviceHealth.filter((service) => service.state === HEALTH_STATE.Degraded).length,
      down: serviceHealth.filter((service) => service.state === HEALTH_STATE.Down).length
    };
  });

  selectScenario(scenarioId: string): void {
    const selectedIndex = SNAPSHOTS.findIndex((snapshot) => snapshot.scenario.id === scenarioId);

    if (selectedIndex < 0) {
      throw new Error(`Unknown simulation scenario: ${scenarioId}`);
    }

    this.snapshotIndex.set(selectedIndex);
  }

  advanceScenario(): void {
    this.snapshotIndex.update((currentIndex) => (currentIndex + 1) % SNAPSHOTS.length);
  }

  startBrokeredBuySimulation(request: StartBrokeredBuySimulationRequest): Promise<StartBrokeredBuySimulationResponse> {
    return firstValueFrom(
      this.apiClient.post<StartBrokeredBuySimulationResponse, StartBrokeredBuySimulationRequest>(
        START_BROKERED_BUY_SIMULATION_ENDPOINT,
        request
      )
    ).then((response) => ensureStartBrokeredBuySimulationResponse(response, START_BROKERED_BUY_SIMULATION_ENDPOINT));
  }

  resetBrokeredBuyData(): Promise<ResetBrokeredBuySimulationDataResponse> {
    return firstValueFrom(
      this.apiClient.post<ResetBrokeredBuySimulationDataResponse, Record<string, never>>(
        RESET_BROKERED_BUY_SIMULATION_DATA_ENDPOINT,
        {}
      )
    ).then((response) => ensureResetBrokeredBuySimulationDataResponse(response, RESET_BROKERED_BUY_SIMULATION_DATA_ENDPOINT));
  }
}

function ensureStartBrokeredBuySimulationResponse(
  response: unknown,
  endpoint: string
): StartBrokeredBuySimulationResponse {
  if (!isStartBrokeredBuySimulationResponse(response)) {
    throw new Error(`Unexpected response from ${endpoint}.`);
  }

  return response;
}

function isStartBrokeredBuySimulationResponse(value: unknown): value is StartBrokeredBuySimulationResponse {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<StartBrokeredBuySimulationResponse>;
  return (
    typeof candidate.correlationId === 'string' &&
    typeof candidate.quoteId === 'string' &&
    typeof candidate.clientOrderId === 'string' &&
    typeof candidate.customerAccountId === 'string' &&
    typeof candidate.assetSymbol === 'string' &&
    typeof candidate.quantity === 'number' &&
    typeof candidate.quoteCurrency === 'string' &&
    typeof candidate.seededCustomerFiatAvailableBalance === 'number' &&
    typeof candidate.seededPlatformBitcoinInventory === 'number' &&
    typeof candidate.seededPlatformEtherInventory === 'number' &&
    typeof candidate.startedAtUtc === 'string' &&
    typeof candidate.status === 'string'
  );
}

function ensureResetBrokeredBuySimulationDataResponse(
  response: unknown,
  endpoint: string
): ResetBrokeredBuySimulationDataResponse {
  if (!isResetBrokeredBuySimulationDataResponse(response)) {
    throw new Error(`Unexpected response from ${endpoint}.`);
  }

  return response;
}

function isResetBrokeredBuySimulationDataResponse(value: unknown): value is ResetBrokeredBuySimulationDataResponse {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<ResetBrokeredBuySimulationDataResponse>;
  return typeof candidate.resetAtUtc === 'string' && typeof candidate.status === 'string';
}
