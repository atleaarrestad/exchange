import { Inject, Injectable, Optional } from '@angular/core';
import { ApiAuthTokenProvider, ApiClient, ApiClientLogger, ObservableApiClientAdapter } from '@exchange/shared-api-client';
import {
  BROKER_WEB_API_AUTH_TOKEN_PROVIDER,
  BROKER_WEB_API_CLIENT_LOGGER,
  createBrokerWebApiClientConfig
} from './broker-web-api-client.config';

@Injectable({ providedIn: 'root' })
export class BrokerWebApiClientService extends ObservableApiClientAdapter {
  constructor(
    @Optional() @Inject(BROKER_WEB_API_CLIENT_LOGGER) logger: ApiClientLogger | null,
    @Optional() @Inject(BROKER_WEB_API_AUTH_TOKEN_PROVIDER) authTokenProvider: ApiAuthTokenProvider | null
  ) {
    super(
      new ApiClient({
        config: createBrokerWebApiClientConfig(),
        logger: logger ?? undefined,
        authTokenProvider: authTokenProvider ?? undefined
      })
    );
  }
}
