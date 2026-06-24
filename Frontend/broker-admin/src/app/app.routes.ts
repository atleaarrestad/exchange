import { Routes } from '@angular/router';
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
  }
];
