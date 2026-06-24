import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import {
  CryptoGatewayResilienceSettingsProfile,
  DEFAULT_CRYPTO_GATEWAY_RESILIENCE_SETTINGS_REQUEST,
  UpsertCryptoGatewayResilienceSettingsRequest,
  toUpsertResilienceRequest
} from './crypto-gateway-resilience-settings.models';
import { CryptoGatewayResilienceSettingsService } from './crypto-gateway-resilience-settings.service';
import {
  CryptoGatewaySettingsProfile,
  DEFAULT_CRYPTO_GATEWAY_SETTINGS_REQUEST,
  GATEWAY_PROVIDER,
  SaveCryptoGatewayCredentialsRequest,
  UpsertCryptoGatewaySettingsRequest,
  toUpsertGatewayRequest
} from './crypto-gateway-settings.models';
import { CryptoGatewaySettingsService } from './crypto-gateway-settings.service';
import {
  CryptoSettingsProfile,
  DEFAULT_CRYPTO_SETTINGS_REQUEST,
  UpsertCryptoSettingsRequest,
  toUpsertRequest
} from './crypto-settings.models';
import { CryptoSettingsService } from './crypto-settings.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule
  ],
  templateUrl: './settings.page.html',
  styleUrl: './settings.page.scss'
})
export class SettingsPageComponent implements OnInit {
  private readonly cryptoSettingsService = inject(CryptoSettingsService);
  private readonly gatewaySettingsService = inject(CryptoGatewaySettingsService);
  private readonly gatewayResilienceSettingsService = inject(CryptoGatewayResilienceSettingsService);

  protected readonly gatewayProviders = Object.values(GATEWAY_PROVIDER);

  protected coreProfiles: readonly CryptoSettingsProfile[] = [];
  protected selectedCoreProfileId: string | null = null;
  protected coreDraft: UpsertCryptoSettingsRequest = cloneDefaultCoreRequest();
  protected isCoreLoading = false;
  protected isCoreSaving = false;
  protected isCoreDeleting = false;
  protected coreErrorMessage: string | null = null;
  protected coreSuccessMessage: string | null = null;

  protected gatewayProfiles: readonly CryptoGatewaySettingsProfile[] = [];
  protected selectedGatewayProfileId: string | null = null;
  protected gatewayDraft: UpsertCryptoGatewaySettingsRequest = cloneDefaultGatewayRequest();
  protected isGatewayLoading = false;
  protected isGatewaySaving = false;
  protected isGatewayDeleting = false;
  protected isGatewayCredentialSaving = false;
  protected gatewayErrorMessage: string | null = null;
  protected gatewaySuccessMessage: string | null = null;
  protected gatewayCredentialsDraft: SaveCryptoGatewayCredentialsRequest = { apiKey: '', apiSecret: '' };

  protected resilienceProfiles: readonly CryptoGatewayResilienceSettingsProfile[] = [];
  protected selectedResilienceProfileId: string | null = null;
  protected resilienceDraft: UpsertCryptoGatewayResilienceSettingsRequest = cloneDefaultResilienceRequest();
  protected isResilienceLoading = false;
  protected isResilienceSaving = false;
  protected isResilienceDeleting = false;
  protected resilienceErrorMessage: string | null = null;
  protected resilienceSuccessMessage: string | null = null;

  async ngOnInit(): Promise<void> {
    await Promise.all([this.refreshCoreProfiles(), this.refreshGatewayProfiles(), this.refreshResilienceProfiles()]);
  }

  protected async refreshCoreProfiles(preferredProfileId?: string | null): Promise<void> {
    this.isCoreLoading = true;
    this.coreErrorMessage = null;

    try {
      const profiles = await this.cryptoSettingsService.listProfiles();
      this.coreProfiles = profiles;

      if (profiles.length === 0) {
        this.selectedCoreProfileId = null;
        this.coreDraft = cloneDefaultCoreRequest();
        return;
      }

      const selected = preferredProfileId ?? this.selectedCoreProfileId ?? profiles[0].id;
      const matched = profiles.find((profile) => profile.id === selected) ?? profiles[0];
      this.selectedCoreProfileId = matched.id;
      this.coreDraft = toUpsertRequest(matched);
    } catch (error) {
      this.coreErrorMessage = getErrorMessage(error);
    } finally {
      this.isCoreLoading = false;
    }
  }

  protected async refreshGatewayProfiles(preferredProfileId?: string | null): Promise<void> {
    this.isGatewayLoading = true;
    this.gatewayErrorMessage = null;

    try {
      const profiles = await this.gatewaySettingsService.listProfiles();
      this.gatewayProfiles = profiles;

      if (profiles.length === 0) {
        this.selectedGatewayProfileId = null;
        this.gatewayDraft = cloneDefaultGatewayRequest();
        return;
      }

      const selected = preferredProfileId ?? this.selectedGatewayProfileId ?? profiles[0].id;
      const matched = profiles.find((profile) => profile.id === selected) ?? profiles[0];
      this.selectedGatewayProfileId = matched.id;
      this.gatewayDraft = toUpsertGatewayRequest(matched);
    } catch (error) {
      this.gatewayErrorMessage = getErrorMessage(error);
    } finally {
      this.isGatewayLoading = false;
    }
  }

  protected onCoreProfileChanged(event: MatSelectChange): void {
    this.selectCoreProfile(String(event.value));
  }

  protected onGatewayProfileChanged(event: MatSelectChange): void {
    this.selectGatewayProfile(String(event.value));
  }

  protected onResilienceProfileChanged(event: MatSelectChange): void {
    this.selectResilienceProfile(String(event.value));
  }

  protected selectCoreProfile(id: string): void {
    const selected = this.coreProfiles.find((profile) => profile.id === id);
    if (selected === undefined) {
      return;
    }

    this.selectedCoreProfileId = selected.id;
    this.coreDraft = toUpsertRequest(selected);
    this.coreErrorMessage = null;
    this.coreSuccessMessage = null;
  }

  protected selectGatewayProfile(id: string): void {
    const selected = this.gatewayProfiles.find((profile) => profile.id === id);
    if (selected === undefined) {
      return;
    }

    this.selectedGatewayProfileId = selected.id;
    this.gatewayDraft = toUpsertGatewayRequest(selected);
    this.gatewayCredentialsDraft = { apiKey: '', apiSecret: '' };
    this.gatewayErrorMessage = null;
    this.gatewaySuccessMessage = null;
  }

  protected selectResilienceProfile(id: string): void {
    const selected = this.resilienceProfiles.find((profile) => profile.id === id);
    if (selected === undefined) {
      return;
    }

    this.selectedResilienceProfileId = selected.id;
    this.resilienceDraft = toUpsertResilienceRequest(selected);
    this.resilienceErrorMessage = null;
    this.resilienceSuccessMessage = null;
  }

  protected beginCoreCreateMode(): void {
    this.selectedCoreProfileId = null;
    this.coreDraft = cloneDefaultCoreRequest();
    this.coreErrorMessage = null;
    this.coreSuccessMessage = null;
  }

  protected beginGatewayCreateMode(): void {
    this.selectedGatewayProfileId = null;
    this.gatewayDraft = cloneDefaultGatewayRequest();
    this.gatewayCredentialsDraft = { apiKey: '', apiSecret: '' };
    this.gatewayErrorMessage = null;
    this.gatewaySuccessMessage = null;
  }

  protected beginResilienceCreateMode(): void {
    this.selectedResilienceProfileId = null;
    this.resilienceDraft = cloneDefaultResilienceRequest();
    this.resilienceErrorMessage = null;
    this.resilienceSuccessMessage = null;
  }

  protected async createCoreProfile(): Promise<void> {
    this.isCoreSaving = true;
    this.coreErrorMessage = null;
    this.coreSuccessMessage = null;

    try {
      const created = await this.cryptoSettingsService.createProfile(this.coreDraft);
      await this.refreshCoreProfiles(created.id);
      this.coreSuccessMessage = `Created profile "${created.name}".`;
    } catch (error) {
      this.coreErrorMessage = getErrorMessage(error);
    } finally {
      this.isCoreSaving = false;
    }
  }

  protected async createGatewayProfile(): Promise<void> {
    this.isGatewaySaving = true;
    this.gatewayErrorMessage = null;
    this.gatewaySuccessMessage = null;

    try {
      const created = await this.gatewaySettingsService.createProfile(this.gatewayDraft);
      await this.refreshGatewayProfiles(created.id);
      this.gatewaySuccessMessage = `Created gateway profile "${created.name}".`;
    } catch (error) {
      this.gatewayErrorMessage = getErrorMessage(error);
    } finally {
      this.isGatewaySaving = false;
    }
  }

  protected async createResilienceProfile(): Promise<void> {
    this.isResilienceSaving = true;
    this.resilienceErrorMessage = null;
    this.resilienceSuccessMessage = null;

    try {
      const created = await this.gatewayResilienceSettingsService.createProfile(this.resilienceDraft);
      await this.refreshResilienceProfiles(created.id);
      this.resilienceSuccessMessage = `Created resilience profile "${created.name}".`;
    } catch (error) {
      this.resilienceErrorMessage = getErrorMessage(error);
    } finally {
      this.isResilienceSaving = false;
    }
  }

  protected async updateSelectedCoreProfile(): Promise<void> {
    if (this.selectedCoreProfileId === null) {
      this.coreErrorMessage = 'Select a profile before updating.';
      return;
    }

    this.isCoreSaving = true;
    this.coreErrorMessage = null;
    this.coreSuccessMessage = null;

    try {
      const updated = await this.cryptoSettingsService.updateProfile(this.selectedCoreProfileId, this.coreDraft);
      await this.refreshCoreProfiles(updated.id);
      this.coreSuccessMessage = `Updated profile "${updated.name}".`;
    } catch (error) {
      this.coreErrorMessage = getErrorMessage(error);
    } finally {
      this.isCoreSaving = false;
    }
  }

  protected async updateSelectedGatewayProfile(): Promise<void> {
    if (this.selectedGatewayProfileId === null) {
      this.gatewayErrorMessage = 'Select a gateway profile before updating.';
      return;
    }

    this.isGatewaySaving = true;
    this.gatewayErrorMessage = null;
    this.gatewaySuccessMessage = null;

    try {
      const updated = await this.gatewaySettingsService.updateProfile(this.selectedGatewayProfileId, this.gatewayDraft);
      await this.refreshGatewayProfiles(updated.id);
      this.gatewaySuccessMessage = `Updated gateway profile "${updated.name}".`;
    } catch (error) {
      this.gatewayErrorMessage = getErrorMessage(error);
    } finally {
      this.isGatewaySaving = false;
    }
  }

  protected async updateSelectedResilienceProfile(): Promise<void> {
    if (this.selectedResilienceProfileId === null) {
      this.resilienceErrorMessage = 'Select a resilience profile before updating.';
      return;
    }

    this.isResilienceSaving = true;
    this.resilienceErrorMessage = null;
    this.resilienceSuccessMessage = null;

    try {
      const updated = await this.gatewayResilienceSettingsService.updateProfile(this.selectedResilienceProfileId, this.resilienceDraft);
      await this.refreshResilienceProfiles(updated.id);
      this.resilienceSuccessMessage = `Updated resilience profile "${updated.name}".`;
    } catch (error) {
      this.resilienceErrorMessage = getErrorMessage(error);
    } finally {
      this.isResilienceSaving = false;
    }
  }

  protected async saveGatewayCredentials(): Promise<void> {
    if (this.selectedGatewayProfileId === null) {
      this.gatewayErrorMessage = 'Select a gateway profile before saving credentials.';
      return;
    }

    this.isGatewayCredentialSaving = true;
    this.gatewayErrorMessage = null;
    this.gatewaySuccessMessage = null;

    try {
      await this.gatewaySettingsService.saveCredentials(this.selectedGatewayProfileId, this.gatewayCredentialsDraft);
      this.gatewayCredentialsDraft = { apiKey: '', apiSecret: '' };
      this.gatewaySuccessMessage = 'Saved new gateway credentials.';
    } catch (error) {
      this.gatewayErrorMessage = getErrorMessage(error);
    } finally {
      this.isGatewayCredentialSaving = false;
    }
  }

  protected async deleteSelectedCoreProfile(): Promise<void> {
    if (this.selectedCoreProfileId === null) {
      this.coreErrorMessage = 'Select a profile before deleting.';
      return;
    }

    this.isCoreDeleting = true;
    this.coreErrorMessage = null;
    this.coreSuccessMessage = null;

    try {
      await this.cryptoSettingsService.deleteProfile(this.selectedCoreProfileId);
      await this.refreshCoreProfiles();
      this.coreSuccessMessage = 'Deleted selected profile.';
    } catch (error) {
      this.coreErrorMessage = getErrorMessage(error);
    } finally {
      this.isCoreDeleting = false;
    }
  }

  protected async deleteSelectedGatewayProfile(): Promise<void> {
    if (this.selectedGatewayProfileId === null) {
      this.gatewayErrorMessage = 'Select a gateway profile before deleting.';
      return;
    }

    this.isGatewayDeleting = true;
    this.gatewayErrorMessage = null;
    this.gatewaySuccessMessage = null;

    try {
      await this.gatewaySettingsService.deleteProfile(this.selectedGatewayProfileId);
      await this.refreshGatewayProfiles();
      this.gatewaySuccessMessage = 'Deleted selected gateway profile.';
    } catch (error) {
      this.gatewayErrorMessage = getErrorMessage(error);
    } finally {
      this.isGatewayDeleting = false;
    }
  }

  protected async deleteSelectedResilienceProfile(): Promise<void> {
    if (this.selectedResilienceProfileId === null) {
      this.resilienceErrorMessage = 'Select a resilience profile before deleting.';
      return;
    }

    this.isResilienceDeleting = true;
    this.resilienceErrorMessage = null;
    this.resilienceSuccessMessage = null;

    try {
      await this.gatewayResilienceSettingsService.deleteProfile(this.selectedResilienceProfileId);
      await this.refreshResilienceProfiles();
      this.resilienceSuccessMessage = 'Deleted selected resilience profile.';
    } catch (error) {
      this.resilienceErrorMessage = getErrorMessage(error);
    } finally {
      this.isResilienceDeleting = false;
    }
  }

  protected async refreshResilienceProfiles(preferredProfileId?: string | null): Promise<void> {
    this.isResilienceLoading = true;
    this.resilienceErrorMessage = null;

    try {
      const profiles = await this.gatewayResilienceSettingsService.listProfiles();
      this.resilienceProfiles = profiles;

      if (profiles.length === 0) {
        this.selectedResilienceProfileId = null;
        this.resilienceDraft = cloneDefaultResilienceRequest();
        return;
      }

      const selected = preferredProfileId ?? this.selectedResilienceProfileId ?? profiles[0].id;
      const matched = profiles.find((profile) => profile.id === selected) ?? profiles[0];
      this.selectedResilienceProfileId = matched.id;
      this.resilienceDraft = toUpsertResilienceRequest(matched);
    } catch (error) {
      this.resilienceErrorMessage = getErrorMessage(error);
    } finally {
      this.isResilienceLoading = false;
    }
  }
}

function cloneDefaultCoreRequest(): UpsertCryptoSettingsRequest {
  return { ...DEFAULT_CRYPTO_SETTINGS_REQUEST };
}

function cloneDefaultGatewayRequest(): UpsertCryptoGatewaySettingsRequest {
  return { ...DEFAULT_CRYPTO_GATEWAY_SETTINGS_REQUEST };
}

function cloneDefaultResilienceRequest(): UpsertCryptoGatewayResilienceSettingsRequest {
  return { ...DEFAULT_CRYPTO_GATEWAY_RESILIENCE_SETTINGS_REQUEST };
}

function getErrorMessage(error: unknown): string {
  if (error instanceof Error && error.message.trim().length > 0) {
    return error.message;
  }

  return 'Request failed.';
}
