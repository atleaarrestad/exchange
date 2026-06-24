import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AdminApiClientService } from '../../core/api/admin-api-client.service';
import {
  CryptoGatewayResilienceSettingsProfile,
  UpsertCryptoGatewayResilienceSettingsRequest
} from './crypto-gateway-resilience-settings.models';

const GATEWAY_RESILIENCE_SETTINGS_ENDPOINT = '/api/admin/crypto-gateway-resilience-settings';

@Injectable({ providedIn: 'root' })
export class CryptoGatewayResilienceSettingsService {
  private readonly apiClient = inject(AdminApiClientService);

  listProfiles(): Promise<readonly CryptoGatewayResilienceSettingsProfile[]> {
    return firstValueFrom(this.apiClient.get<readonly CryptoGatewayResilienceSettingsProfile[]>(GATEWAY_RESILIENCE_SETTINGS_ENDPOINT));
  }

  createProfile(request: UpsertCryptoGatewayResilienceSettingsRequest): Promise<CryptoGatewayResilienceSettingsProfile> {
    return firstValueFrom(
      this.apiClient.post<CryptoGatewayResilienceSettingsProfile, UpsertCryptoGatewayResilienceSettingsRequest>(
        GATEWAY_RESILIENCE_SETTINGS_ENDPOINT,
        request
      )
    );
  }

  updateProfile(
    id: string,
    request: UpsertCryptoGatewayResilienceSettingsRequest
  ): Promise<CryptoGatewayResilienceSettingsProfile> {
    return firstValueFrom(
      this.apiClient.put<CryptoGatewayResilienceSettingsProfile, UpsertCryptoGatewayResilienceSettingsRequest>(
        `${GATEWAY_RESILIENCE_SETTINGS_ENDPOINT}/${id}`,
        request
      )
    );
  }

  async deleteProfile(id: string): Promise<void> {
    await firstValueFrom(this.apiClient.delete<void>(`${GATEWAY_RESILIENCE_SETTINGS_ENDPOINT}/${id}`));
  }
}
