import { NgClass, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { EVENT_SEVERITY, HEALTH_STATE, INVARIANT_STATE } from './simulation-ops.models';
import { SimulationOpsConsoleService } from './simulation-ops-console.service';

@Component({
  selector: 'app-simulation-ops-console-page',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    NgClass,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatToolbarModule
  ],
  templateUrl: './simulation-ops-console.page.html',
  styleUrl: './simulation-ops-console.page.scss'
})
export class SimulationOpsConsolePageComponent {
  protected readonly opsConsole = inject(SimulationOpsConsoleService);
  protected readonly snapshot = this.opsConsole.snapshot;
  protected readonly scenarios = this.opsConsole.scenarios;
  protected readonly healthSummary = this.opsConsole.healthSummary;

  protected readonly healthColumns = ['service', 'state', 'latency', 'queueDepth', 'lastUpdated'] as const;
  protected readonly eventColumns = ['timestamp', 'correlationId', 'step', 'severity', 'detail'] as const;
  protected startRequest = {
    customerAccountId: 'sim-customer-1',
    assetSymbol: 'BTC',
    quantity: 0.1,
    quoteCurrency: 'NOK',
    customerFiatAvailableBalance: 500_000,
    platformBitcoinInventory: 2,
    platformEtherInventory: 20
  };
  protected isStartRunning = false;
  protected isResetRunning = false;
  protected operationErrorMessage: string | null = null;
  protected operationSuccessMessage: string | null = null;
  protected lastOperationAtUtc: string | null = null;

  protected onScenarioChanged(event: MatSelectChange): void {
    this.opsConsole.selectScenario(String(event.value));
  }

  protected showNextScenario(): void {
    this.opsConsole.advanceScenario();
  }

  protected async startSimulation(): Promise<void> {
    this.isStartRunning = true;
    this.operationErrorMessage = null;
    this.operationSuccessMessage = null;

    try {
      const response = await this.opsConsole.startBrokeredBuySimulation({
        customerAccountId: this.startRequest.customerAccountId,
        assetSymbol: this.startRequest.assetSymbol,
        quantity: this.startRequest.quantity,
        quoteCurrency: this.startRequest.quoteCurrency,
        customerFiatAvailableBalance: this.startRequest.customerFiatAvailableBalance,
        platformBitcoinInventory: this.startRequest.platformBitcoinInventory,
        platformEtherInventory: this.startRequest.platformEtherInventory,
        maxUnitPrice: null,
        maxTotalCost: null,
        clientOrderId: null
      });
      this.lastOperationAtUtc = response.startedAtUtc;
      this.operationSuccessMessage = `Started simulation order ${response.clientOrderId} (${response.status}).`;
    } catch (error) {
      this.operationErrorMessage = getErrorMessage(error);
    } finally {
      this.isStartRunning = false;
    }
  }

  protected async resetSimulationData(): Promise<void> {
    this.isResetRunning = true;
    this.operationErrorMessage = null;
    this.operationSuccessMessage = null;

    try {
      const response = await this.opsConsole.resetBrokeredBuyData();
      this.lastOperationAtUtc = response.resetAtUtc;
      this.operationSuccessMessage = `Reset simulation data (${response.status}).`;
    } catch (error) {
      this.operationErrorMessage = getErrorMessage(error);
    } finally {
      this.isResetRunning = false;
    }
  }

  protected statusClass(status: string): string {
    switch (status) {
      case HEALTH_STATE.Healthy:
      case INVARIANT_STATE.Passing:
      case EVENT_SEVERITY.Info:
        return 'status--good';
      case HEALTH_STATE.Degraded:
      case INVARIANT_STATE.Warning:
      case EVENT_SEVERITY.Warning:
        return 'status--warn';
      case HEALTH_STATE.Down:
      case INVARIANT_STATE.Failed:
      case EVENT_SEVERITY.Error:
        return 'status--bad';
      default:
        return '';
    }
  }
}

function getErrorMessage(error: unknown): string {
  if (error instanceof Error && error.message.trim().length > 0) {
    return error.message;
  }

  return 'Request failed.';
}
