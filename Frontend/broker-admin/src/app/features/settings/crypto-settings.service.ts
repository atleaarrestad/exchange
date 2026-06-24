import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AdminApiClientService } from '../../core/api/admin-api-client.service';
import { CryptoSettingsProfile, UpsertCryptoSettingsRequest } from './crypto-settings.models';

const CRYPTO_SETTINGS_ENDPOINT = '/api/admin/crypto-settings';

@Injectable({ providedIn: 'root' })
export class CryptoSettingsService {
  private readonly apiClient = inject(AdminApiClientService);

  listProfiles(): Promise<readonly CryptoSettingsProfile[]> {
    return firstValueFrom(this.apiClient.get<readonly CryptoSettingsProfile[]>(CRYPTO_SETTINGS_ENDPOINT));
  }

  createProfile(request: UpsertCryptoSettingsRequest): Promise<CryptoSettingsProfile> {
    return firstValueFrom(this.apiClient.post<CryptoSettingsProfile, UpsertCryptoSettingsRequest>(CRYPTO_SETTINGS_ENDPOINT, request));
  }

  updateProfile(id: string, request: UpsertCryptoSettingsRequest): Promise<CryptoSettingsProfile> {
    return firstValueFrom(this.apiClient.put<CryptoSettingsProfile, UpsertCryptoSettingsRequest>(`${CRYPTO_SETTINGS_ENDPOINT}/${id}`, request));
  }

  async deleteProfile(id: string): Promise<void> {
    await firstValueFrom(this.apiClient.delete<void>(`${CRYPTO_SETTINGS_ENDPOINT}/${id}`));
  }
}
