import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AdminApiClientService } from '../../core/api/admin-api-client.service';
import {
  CryptoGatewaySettingsProfile,
  SaveCryptoGatewayCredentialsRequest,
  UpsertCryptoGatewaySettingsRequest
} from './crypto-gateway-settings.models';

const GATEWAY_SETTINGS_ENDPOINT = '/api/admin/crypto-gateway-settings';

@Injectable({ providedIn: 'root' })
export class CryptoGatewaySettingsService {
  private readonly apiClient = inject(AdminApiClientService);

  listProfiles(): Promise<readonly CryptoGatewaySettingsProfile[]> {
    return firstValueFrom(this.apiClient.get<readonly CryptoGatewaySettingsProfile[]>(GATEWAY_SETTINGS_ENDPOINT));
  }

  createProfile(request: UpsertCryptoGatewaySettingsRequest): Promise<CryptoGatewaySettingsProfile> {
    return firstValueFrom(
      this.apiClient.post<CryptoGatewaySettingsProfile, UpsertCryptoGatewaySettingsRequest>(GATEWAY_SETTINGS_ENDPOINT, request)
    );
  }

  updateProfile(id: string, request: UpsertCryptoGatewaySettingsRequest): Promise<CryptoGatewaySettingsProfile> {
    return firstValueFrom(
      this.apiClient.put<CryptoGatewaySettingsProfile, UpsertCryptoGatewaySettingsRequest>(`${GATEWAY_SETTINGS_ENDPOINT}/${id}`, request)
    );
  }

  async saveCredentials(id: string, request: SaveCryptoGatewayCredentialsRequest): Promise<void> {
    await firstValueFrom(this.apiClient.put<void, SaveCryptoGatewayCredentialsRequest>(`${GATEWAY_SETTINGS_ENDPOINT}/${id}/credentials`, request));
  }

  async deleteProfile(id: string): Promise<void> {
    await firstValueFrom(this.apiClient.delete<void>(`${GATEWAY_SETTINGS_ENDPOINT}/${id}`));
  }
}
