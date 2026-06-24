import { Inject, Injectable, Optional } from '@angular/core';
import { ApiAuthTokenProvider, ApiClient, ApiClientLogger, ObservableApiClientAdapter } from '@exchange/shared-api-client';
import { ADMIN_API_AUTH_TOKEN_PROVIDER, ADMIN_API_CLIENT_LOGGER, createAdminApiClientConfig } from './admin-api-client.config';

@Injectable({ providedIn: 'root' })
export class AdminApiClientService extends ObservableApiClientAdapter {
  constructor(
    @Optional() @Inject(ADMIN_API_CLIENT_LOGGER) logger: ApiClientLogger | null,
    @Optional() @Inject(ADMIN_API_AUTH_TOKEN_PROVIDER) authTokenProvider: ApiAuthTokenProvider | null
  ) {
    super(
      new ApiClient({
        config: createAdminApiClientConfig(),
        logger: logger ?? undefined,
        authTokenProvider: authTokenProvider ?? undefined
      })
    );
  }
}
