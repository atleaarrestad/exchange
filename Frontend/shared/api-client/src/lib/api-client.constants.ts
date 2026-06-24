export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export const HTTP_METHOD = {
  Get: 'GET',
  Post: 'POST',
  Put: 'PUT',
  Patch: 'PATCH',
  Delete: 'DELETE'
} as const satisfies Record<string, HttpMethod>;

export const HEADER_NAME = {
  Authorization: 'Authorization',
  ContentType: 'Content-Type',
  CorrelationId: 'X-Correlation-Id'
} as const;

export const CONTENT_TYPE = {
  ApplicationJson: 'application/json'
} as const;

export const DEFAULT_TIMEOUT_MS = 10_000;
export const DEFAULT_RETRY_MAX_ATTEMPTS = 2;
export const DEFAULT_RETRY_BASE_DELAY_MS = 250;
export const DEFAULT_RETRY_MAX_DELAY_MS = 2_000;
export const DEFAULT_RETRY_JITTER_FACTOR = 0.2;
export const DEFAULT_RETRYABLE_STATUS_CODES: readonly number[] = [0, 408, 425, 429, 500, 502, 503, 504];
export const DEFAULT_RETRYABLE_METHODS: readonly HttpMethod[] = [HTTP_METHOD.Get];
