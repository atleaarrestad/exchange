import { InjectionToken } from '@angular/core';
import { ApiAuthTokenProvider, ApiClientConfig, ApiClientLogger } from '@exchange/shared-api-client';

const ADMIN_API_BASE_URL_GLOBAL = '__EXCHANGE_ADMIN_API_BASE_URL__';
const BROWSER_ORIGIN_FALLBACK = 'http://localhost';

declare global {
  interface Window {
    __EXCHANGE_ADMIN_API_BASE_URL__?: string;
  }
}

export const ADMIN_API_CLIENT_LOGGER = new InjectionToken<ApiClientLogger>('ADMIN_API_CLIENT_LOGGER');
export const ADMIN_API_AUTH_TOKEN_PROVIDER = new InjectionToken<ApiAuthTokenProvider>('ADMIN_API_AUTH_TOKEN_PROVIDER');

function resolveAdminApiBaseUrl(): string {
  if (typeof window === 'undefined') {
    return BROWSER_ORIGIN_FALLBACK;
  }

  const configuredBaseUrl = window[ADMIN_API_BASE_URL_GLOBAL]?.trim();
  if (configuredBaseUrl !== undefined && configuredBaseUrl.length > 0) {
    return configuredBaseUrl;
  }

  return window.location.origin;
}

export function createAdminApiClientConfig(): ApiClientConfig {
  return {
    baseUrl: resolveAdminApiBaseUrl(),
    timeoutMs: 10_000
  };
}
