import { Routes } from '@angular/router';
import { CronJobLogsPageComponent } from './features/cron-jobs/cron-job-logs.page';
import { CronJobsPageComponent } from './features/cron-jobs/cron-jobs.page';
import { SimulationOpsConsolePageComponent } from './features/simulation-ops/simulation-ops-console.page';
import { SettingsPageComponent } from './features/settings/settings.page';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'simulation-ops'
  },
  {
    path: 'simulation-ops',
    component: SimulationOpsConsolePageComponent
  },
  {
    path: 'settings',
    component: SettingsPageComponent
  },
  {
    path: 'cron-jobs/:jobName/logs',
    component: CronJobLogsPageComponent
  },
  {
    path: 'cron-jobs',
    component: CronJobsPageComponent
  }
];
