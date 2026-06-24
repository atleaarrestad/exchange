import { NgClass, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
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
    NgClass,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
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

  protected onScenarioChanged(event: MatSelectChange): void {
    this.opsConsole.selectScenario(String(event.value));
  }

  protected showNextScenario(): void {
    this.opsConsole.advanceScenario();
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
