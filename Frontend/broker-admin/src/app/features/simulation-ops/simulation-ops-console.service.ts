import { Injectable, computed, signal } from '@angular/core';
import {
  EVENT_SEVERITY,
  HEALTH_STATE,
  INVARIANT_STATE,
  OPERATING_ENVIRONMENT,
  OpsScenario,
  OpsSnapshot
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
}
