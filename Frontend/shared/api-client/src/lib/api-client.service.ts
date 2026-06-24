import {
  CONTENT_TYPE,
  DEFAULT_RETRY_BASE_DELAY_MS,
  DEFAULT_RETRY_JITTER_FACTOR,
  DEFAULT_RETRY_MAX_ATTEMPTS,
  DEFAULT_RETRY_MAX_DELAY_MS,
  DEFAULT_RETRYABLE_METHODS,
  DEFAULT_RETRYABLE_STATUS_CODES,
  DEFAULT_TIMEOUT_MS,
  HEADER_NAME,
  HTTP_METHOD,
  HttpMethod
} from './api-client.constants';
import { ApiClientError } from './api-client.errors';
import { ApiAuthTokenProvider, ApiClientConfig, ApiClientLogger, ApiClientRetryPolicy, ApiQueryParams, ApiQueryValue, ApiRequestOptions, ResolvedApiClientConfig } from './api-client.models';

export interface ApiClientOptions {
  readonly config: ApiClientConfig;
  readonly logger?: ApiClientLogger;
  readonly authTokenProvider?: ApiAuthTokenProvider;
}

export class ApiClient {
  private readonly config: ResolvedApiClientConfig;
  private readonly logger: ApiClientLogger | undefined;
  private readonly authTokenProvider: ApiAuthTokenProvider | undefined;

  constructor(options: ApiClientOptions) {
    this.config = resolveConfig(options.config);
    this.logger = options.logger;
    this.authTokenProvider = options.authTokenProvider;
  }

  get<TResponse>(path: string, options?: Omit<ApiRequestOptions<never>, 'body'>): Promise<TResponse> {
    return this.request<TResponse, never>(HTTP_METHOD.Get, path, options);
  }

  post<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body'>
  ): Promise<TResponse> {
    return this.request<TResponse, TBody>(HTTP_METHOD.Post, path, { ...options, body });
  }

  put<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body'>
  ): Promise<TResponse> {
    return this.request<TResponse, TBody>(HTTP_METHOD.Put, path, { ...options, body });
  }

  patch<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body'>
  ): Promise<TResponse> {
    return this.request<TResponse, TBody>(HTTP_METHOD.Patch, path, { ...options, body });
  }

  delete<TResponse>(path: string, options?: Omit<ApiRequestOptions<never>, 'body'>): Promise<TResponse> {
    return this.request<TResponse, never>(HTTP_METHOD.Delete, path, options);
  }

  async request<TResponse, TBody>(
    method: HttpMethod,
    path: string,
    options: ApiRequestOptions<TBody> = {}
  ): Promise<TResponse> {
    const correlationId = createCorrelationId();
    const requestUrl = resolveRequestUrl(this.config.baseUrl, path, options.query);
    const retryPolicy = resolveRetryPolicy(this.config.retryPolicy, options.retryPolicy);
    const timeoutMs = options.timeoutMs ?? this.config.timeoutMs;
    const startedAtEpochMs = Date.now();

    this.logger?.onRequestStart?.({ correlationId, method, url: requestUrl });

    for (let attempt = 0; ; attempt += 1) {
      try {
        const authToken = await this.resolveAuthToken(options, requestUrl);
        const request = new Request(requestUrl, {
          method,
          body: serializeRequestBody(options.body),
          headers: buildHeaders(this.config, options, correlationId, authToken),
          credentials: options.credentials ?? (this.config.withCredentials ? 'include' : 'same-origin'),
          signal: options.signal
        });

        const response = await fetchWithTimeout(request, timeoutMs, options.signal);
        const responseBody = await parseResponseBody(response, requestUrl, correlationId);

        if (!response.ok) {
          throw new ApiClientError({
            message: response.statusText || `API request failed with status ${response.status}.`,
            statusCode: response.status,
            responseBody,
            requestUrl,
            correlationId,
            cause: responseBody
          });
        }

        this.logger?.onRequestSuccess?.({
          correlationId,
          method,
          url: requestUrl,
          statusCode: response.status,
          durationMs: Date.now() - startedAtEpochMs
        });

        return responseBody as TResponse;
      } catch (error) {
        const statusCode = error instanceof ApiClientError ? error.statusCode : undefined;
        const normalizedError =
          error instanceof ApiClientError
            ? error
            : normalizeUnknownRequestError(error, requestUrl, correlationId);

        const canRetry =
          !isAbortError(error) &&
          !isResponseParseError(normalizedError) &&
          shouldRetry(attempt, method, statusCode, retryPolicy) &&
          (statusCode !== undefined || isTransientTransportError(normalizedError));
        if (!canRetry) {
          this.logger?.onRequestFailure?.({
            correlationId,
            method,
            url: requestUrl,
            statusCode,
            durationMs: Date.now() - startedAtEpochMs,
            error: normalizedError
          });

          throw normalizedError;
        }

        const delayMs = computeRetryDelayMs(retryPolicy, attempt);
        const wasAbortedDuringDelay = await delay(delayMs, options.signal);
        if (wasAbortedDuringDelay) {
          const abortedError = normalizeUnknownRequestError(
            createAbortError('API request was aborted during retry backoff.'),
            requestUrl,
            correlationId
          );

          this.logger?.onRequestFailure?.({
            correlationId,
            method,
            url: requestUrl,
            statusCode,
            durationMs: Date.now() - startedAtEpochMs,
            error: abortedError
          });

          throw abortedError;
        }
      }
    }
  }

  private async resolveAuthToken<TBody>(options: ApiRequestOptions<TBody>, requestUrl: string): Promise<string | null> {
    if (options.includeAuth === false) {
      return null;
    }

    if (options.includeAuth !== true && !isSameOriginRequest(this.config.baseUrl, requestUrl)) {
      return null;
    }

    if (options.authToken !== undefined) {
      return options.authToken;
    }

    if (this.authTokenProvider === undefined) {
      return null;
    }

    return this.authTokenProvider();
  }
}

function buildHeaders<TBody>(
  config: ResolvedApiClientConfig,
  options: ApiRequestOptions<TBody>,
  correlationId: string,
  authToken: string | null
): Headers {
  const headers = new Headers(options.skipDefaultHeaders ? {} : config.defaultHeaders);

  if (options.headers !== undefined) {
    for (const [key, value] of Object.entries(options.headers)) {
      headers.set(key, value);
    }
  }

  if (config.enableCorrelationId) {
    headers.set(config.correlationIdHeaderName, correlationId);
  }

  if (authToken !== null && authToken.length > 0 && !headers.has(HEADER_NAME.Authorization)) {
    headers.set(HEADER_NAME.Authorization, `Bearer ${authToken}`);
  }

  if (
    options.body !== undefined &&
    options.body !== null &&
    !isRawBody(options.body) &&
    !headers.has(HEADER_NAME.ContentType)
  ) {
    headers.set(HEADER_NAME.ContentType, CONTENT_TYPE.ApplicationJson);
  }

  return headers;
}

function serializeRequestBody<TBody>(body: TBody | undefined): BodyInit | undefined {
  if (body === undefined || body === null) {
    return undefined;
  }

  if (isRawBody(body)) {
    return body;
  }

  return JSON.stringify(body);
}

function isRawBody(body: unknown): body is string | FormData | URLSearchParams | Blob | ArrayBuffer {
  return (
    typeof body === 'string' ||
    body instanceof FormData ||
    body instanceof URLSearchParams ||
    body instanceof Blob ||
    body instanceof ArrayBuffer
  );
}

async function parseResponseBody(response: Response, requestUrl: string, correlationId: string): Promise<unknown> {
  if (response.status === 204 || response.status === 205) {
    return undefined;
  }

  const responseText = await response.text();
  if (responseText.length === 0) {
    return undefined;
  }

  const contentType = response.headers.get(HEADER_NAME.ContentType) ?? '';
  if (isJsonContentType(contentType)) {
    try {
      return JSON.parse(responseText) as unknown;
    } catch (error) {
      throw new ApiClientError({
        message: 'Invalid JSON response payload.',
        statusCode: response.status,
        responseBody: responseText,
        requestUrl,
        correlationId,
        cause: error
      });
    }
  }

  return responseText;
}

function isJsonContentType(contentType: string): boolean {
  const normalizedContentType = contentType.toLowerCase();
  return normalizedContentType.includes(CONTENT_TYPE.ApplicationJson) || normalizedContentType.includes('+json');
}

function resolveRequestUrl(baseUrl: string, path: string, queryParams: ApiQueryParams | undefined): string {
  const rawUrl = /^https?:\/\//.test(path)
    ? path
    : `${baseUrl}/${path.startsWith('/') ? path.slice(1) : path}`;

  const url = new URL(rawUrl);

  if (queryParams !== undefined) {
    for (const [key, value] of Object.entries(queryParams)) {
      if (value === undefined || value === null) {
        continue;
      }

      const values = Array.isArray(value) ? value : [value];
      for (const item of values) {
        url.searchParams.append(key, formatQueryValue(item));
      }
    }
  }

  return url.toString();
}

function formatQueryValue(value: ApiQueryValue): string {
  if (value instanceof Date) {
    return value.toISOString();
  }

  return String(value);
}

function isSameOriginRequest(baseUrl: string, requestUrl: string): boolean {
  return new URL(baseUrl).origin === new URL(requestUrl).origin;
}

function createCorrelationId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

function isAbortError(error: unknown): boolean {
  if (error instanceof DOMException && error.name === 'AbortError') {
    return true;
  }

  if (typeof error === 'object' && error !== null && 'name' in error) {
    return (error as { name?: unknown }).name === 'AbortError';
  }

  return false;
}

function createAbortError(message: string): DOMException | Error {
  if (typeof DOMException !== 'undefined') {
    return new DOMException(message, 'AbortError');
  }

  const abortError = new Error(message);
  abortError.name = 'AbortError';
  return abortError;
}

function isResponseParseError(error: ApiClientError): boolean {
  return error.statusCode !== undefined && error.cause instanceof SyntaxError && error.message === 'Invalid JSON response payload.';
}

async function fetchWithTimeout(request: Request, timeoutMs: number, externalSignal: AbortSignal | undefined): Promise<Response> {
  const timeoutController = new AbortController();
  const timerId = setTimeout(() => timeoutController.abort(), timeoutMs);
  const signal = combineAbortSignals(externalSignal, timeoutController.signal);

  try {
    return await fetch(request, { signal });
  } catch (error) {
    if (timeoutController.signal.aborted && !externalSignal?.aborted) {
      throw new ApiRequestTimeoutError();
    }

    throw error;
  } finally {
    clearTimeout(timerId);
  }
}

function combineAbortSignals(externalSignal: AbortSignal | undefined, timeoutSignal: AbortSignal): AbortSignal {
  if (externalSignal === undefined) {
    return timeoutSignal;
  }

  if (typeof AbortSignal.any === 'function') {
    return AbortSignal.any([externalSignal, timeoutSignal]);
  }

  const combinedController = new AbortController();

  const abortCombinedSignal = (): void => {
    externalSignal.removeEventListener('abort', abortCombinedSignal);
    timeoutSignal.removeEventListener('abort', abortCombinedSignal);
    combinedController.abort();
  };

  if (externalSignal.aborted || timeoutSignal.aborted) {
    abortCombinedSignal();
    return combinedController.signal;
  }

  externalSignal.addEventListener('abort', abortCombinedSignal, { once: true });
  timeoutSignal.addEventListener('abort', abortCombinedSignal, { once: true });

  return combinedController.signal;
}

function resolveConfig(config: ApiClientConfig): ResolvedApiClientConfig {
  const normalizedBaseUrl = normalizeBaseUrl(config.baseUrl);

  return {
    baseUrl: normalizedBaseUrl,
    timeoutMs: coercePositiveInteger(config.timeoutMs, DEFAULT_TIMEOUT_MS),
    defaultHeaders: config.defaultHeaders ?? {},
    withCredentials: config.withCredentials ?? false,
    enableCorrelationId: config.enableCorrelationId ?? true,
    correlationIdHeaderName: config.correlationIdHeaderName ?? HEADER_NAME.CorrelationId,
    retryPolicy: {
      maxAttempts: coercePositiveInteger(config.retryPolicy?.maxAttempts, DEFAULT_RETRY_MAX_ATTEMPTS),
      baseDelayMs: coercePositiveInteger(config.retryPolicy?.baseDelayMs, DEFAULT_RETRY_BASE_DELAY_MS),
      maxDelayMs: coercePositiveInteger(config.retryPolicy?.maxDelayMs, DEFAULT_RETRY_MAX_DELAY_MS),
      jitterFactor: coerceFraction(config.retryPolicy?.jitterFactor, DEFAULT_RETRY_JITTER_FACTOR),
      retryableMethods: config.retryPolicy?.retryableMethods ?? DEFAULT_RETRYABLE_METHODS,
      retryableStatusCodes: config.retryPolicy?.retryableStatusCodes ?? DEFAULT_RETRYABLE_STATUS_CODES
    }
  };
}

function resolveRetryPolicy(
  defaultPolicy: ApiClientRetryPolicy,
  overridePolicy: Partial<ApiClientRetryPolicy> | undefined
): ApiClientRetryPolicy {
  return {
    maxAttempts: coercePositiveInteger(overridePolicy?.maxAttempts, defaultPolicy.maxAttempts),
    baseDelayMs: coercePositiveInteger(overridePolicy?.baseDelayMs, defaultPolicy.baseDelayMs),
    maxDelayMs: coercePositiveInteger(overridePolicy?.maxDelayMs, defaultPolicy.maxDelayMs),
    jitterFactor: coerceFraction(overridePolicy?.jitterFactor, defaultPolicy.jitterFactor),
    retryableMethods: overridePolicy?.retryableMethods ?? defaultPolicy.retryableMethods,
    retryableStatusCodes: overridePolicy?.retryableStatusCodes ?? defaultPolicy.retryableStatusCodes
  };
}

function computeRetryDelayMs(retryPolicy: ApiClientRetryPolicy, attempt: number): number {
  const exponentialDelayMs = Math.min(retryPolicy.baseDelayMs * 2 ** attempt, retryPolicy.maxDelayMs);
  const jitterSpanMs = exponentialDelayMs * retryPolicy.jitterFactor;
  const lowerBoundMs = Math.max(0, exponentialDelayMs - jitterSpanMs);
  const upperBoundMs = exponentialDelayMs + jitterSpanMs;
  return Math.round(lowerBoundMs + (upperBoundMs - lowerBoundMs) * Math.random());
}

function shouldRetry(
  attempt: number,
  method: HttpMethod,
  statusCode: number | undefined,
  retryPolicy: ApiClientRetryPolicy
): boolean {
  if (attempt + 1 >= retryPolicy.maxAttempts) {
    return false;
  }

  if (!retryPolicy.retryableMethods.includes(method)) {
    return false;
  }

  if (statusCode === undefined) {
    return true;
  }

  return retryPolicy.retryableStatusCodes.includes(statusCode);
}

function coercePositiveInteger(value: number | undefined, fallbackValue: number): number {
  if (value === undefined) {
    return fallbackValue;
  }

  if (!Number.isFinite(value) || value < 0) {
    throw new Error(`Expected a non-negative number, received ${value}.`);
  }

  return Math.floor(value);
}

function coerceFraction(value: number | undefined, fallbackValue: number): number {
  if (value === undefined) {
    return fallbackValue;
  }

  if (!Number.isFinite(value) || value < 0 || value > 1) {
    throw new Error(`Expected a number between 0 and 1, received ${value}.`);
  }

  return value;
}

function normalizeBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim();
  if (trimmed.length === 0) {
    throw new Error('API client baseUrl must not be empty.');
  }

  let parsedUrl: URL;
  try {
    parsedUrl = new URL(trimmed);
  } catch {
    throw new Error('API client baseUrl must be an absolute URL.');
  }

  if (parsedUrl.protocol !== 'http:' && parsedUrl.protocol !== 'https:') {
    throw new Error('API client baseUrl must use http or https.');
  }

  const normalizedUrl = parsedUrl.toString();
  return normalizedUrl.endsWith('/') ? normalizedUrl.slice(0, -1) : normalizedUrl;
}

function delay(milliseconds: number, signal?: AbortSignal): Promise<boolean> {
  return new Promise((resolve) => {
    if (signal?.aborted) {
      resolve(true);
      return;
    }

    const timerId = setTimeout(() => {
      signal?.removeEventListener('abort', onAbort);
      resolve(false);
    }, milliseconds);

    const onAbort = (): void => {
      clearTimeout(timerId);
      signal?.removeEventListener('abort', onAbort);
      resolve(true);
    };

    signal?.addEventListener('abort', onAbort, { once: true });
  });
}

function normalizeUnknownRequestError(error: unknown, requestUrl: string, correlationId: string): ApiClientError {
  if (error instanceof ApiRequestTimeoutError) {
    return new ApiClientError({
      message: 'API request timed out.',
      requestUrl,
      correlationId,
      cause: error
    });
  }

  if (isAbortError(error)) {
    return new ApiClientError({
      message: 'API request was aborted.',
      requestUrl,
      correlationId,
      cause: error
    });
  }

  if (error instanceof TypeError) {
    return new ApiClientError({
      message: 'Network error during API request.',
      requestUrl,
      correlationId,
      cause: error
    });
  }

  return new ApiClientError({
    message: 'Unexpected API communication failure.',
    requestUrl,
    correlationId,
    cause: error
  });
}

function isTransientTransportError(error: ApiClientError): boolean {
  return error.cause instanceof ApiRequestTimeoutError || error.cause instanceof TypeError;
}

class ApiRequestTimeoutError extends Error {
  constructor() {
    super('API request timed out.');
    this.name = 'ApiRequestTimeoutError';
  }
}
