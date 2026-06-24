import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [MatTabsModule],
  templateUrl: './settings.page.html',
  styleUrl: './settings.page.scss'
})
export class SettingsPageComponent {
}
