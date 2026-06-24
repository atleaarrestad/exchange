import { InjectionToken } from '@angular/core';
import { ApiAuthTokenProvider, ApiClientConfig, ApiClientLogger } from '@exchange/shared-api-client';

const BROKER_WEB_API_BASE_URL_GLOBAL = '__EXCHANGE_BROKER_WEB_API_BASE_URL__';
const BROWSER_ORIGIN_FALLBACK = 'http://localhost';

declare global {
  interface Window {
    __EXCHANGE_BROKER_WEB_API_BASE_URL__?: string;
  }
}

export const BROKER_WEB_API_CLIENT_LOGGER = new InjectionToken<ApiClientLogger>('BROKER_WEB_API_CLIENT_LOGGER');
export const BROKER_WEB_API_AUTH_TOKEN_PROVIDER = new InjectionToken<ApiAuthTokenProvider>('BROKER_WEB_API_AUTH_TOKEN_PROVIDER');

function resolveBrokerWebApiBaseUrl(): string {
  if (typeof window === 'undefined') {
    return BROWSER_ORIGIN_FALLBACK;
  }

  const configuredBaseUrl = window[BROKER_WEB_API_BASE_URL_GLOBAL]?.trim();
  if (configuredBaseUrl !== undefined && configuredBaseUrl.length > 0) {
    return configuredBaseUrl;
  }

  return window.location.origin;
}

export function createBrokerWebApiClientConfig(): ApiClientConfig {
  return {
    baseUrl: resolveBrokerWebApiBaseUrl(),
    timeoutMs: 10_000
  };
}
