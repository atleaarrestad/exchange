import { HttpMethod } from './api-client.constants';

export type ApiQueryValue = string | number | boolean | Date;
export type ApiQueryParams = Readonly<Record<string, ApiQueryValue | readonly ApiQueryValue[] | null | undefined>>;
export type ApiHeaders = Readonly<Record<string, string>>;
export type ApiAuthTokenProvider = () => string | null | Promise<string | null>;

export interface ApiClientRetryPolicy {
  readonly maxAttempts: number;
  readonly baseDelayMs: number;
  readonly maxDelayMs: number;
  readonly jitterFactor: number;
  readonly retryableMethods: readonly HttpMethod[];
  readonly retryableStatusCodes: readonly number[];
}

export interface ApiClientConfig {
  readonly baseUrl: string;
  readonly timeoutMs?: number;
  readonly defaultHeaders?: ApiHeaders;
  readonly withCredentials?: boolean;
  readonly enableCorrelationId?: boolean;
  readonly correlationIdHeaderName?: string;
  readonly retryPolicy?: Partial<ApiClientRetryPolicy>;
}

export interface ResolvedApiClientConfig {
  readonly baseUrl: string;
  readonly timeoutMs: number;
  readonly defaultHeaders: ApiHeaders;
  readonly withCredentials: boolean;
  readonly enableCorrelationId: boolean;
  readonly correlationIdHeaderName: string;
  readonly retryPolicy: ApiClientRetryPolicy;
}

export interface ApiRequestOptions<TBody> {
  readonly body?: TBody;
  readonly query?: ApiQueryParams;
  readonly headers?: ApiHeaders;
  readonly timeoutMs?: number;
  readonly credentials?: RequestCredentials;
  readonly includeAuth?: boolean;
  readonly authToken?: string | null;
  readonly skipDefaultHeaders?: boolean;
  readonly signal?: AbortSignal;
  readonly retryPolicy?: Partial<ApiClientRetryPolicy>;
}

export interface ApiRequestStartLog {
  readonly correlationId: string;
  readonly method: HttpMethod;
  readonly url: string;
}

export interface ApiRequestSuccessLog {
  readonly correlationId: string;
  readonly method: HttpMethod;
  readonly url: string;
  readonly statusCode: number;
  readonly durationMs: number;
}

export interface ApiRequestFailureLog {
  readonly correlationId: string;
  readonly method: HttpMethod;
  readonly url: string;
  readonly statusCode?: number;
  readonly durationMs: number;
  readonly error: unknown;
}

export interface ApiClientLogger {
  onRequestStart?(event: ApiRequestStartLog): void;
  onRequestSuccess?(event: ApiRequestSuccessLog): void;
  onRequestFailure?(event: ApiRequestFailureLog): void;
}
