export interface ApiClientErrorDetails {
  readonly message: string;
  readonly statusCode?: number;
  readonly responseBody?: unknown;
  readonly requestUrl: string;
  readonly correlationId: string;
  readonly cause: unknown;
}

export class ApiClientError extends Error {
  readonly statusCode?: number;
  readonly responseBody?: unknown;
  readonly requestUrl: string;
  readonly correlationId: string;

  constructor(details: ApiClientErrorDetails) {
    super(details.message, { cause: details.cause });
    this.name = 'ApiClientError';
    this.statusCode = details.statusCode;
    this.responseBody = details.responseBody;
    this.requestUrl = details.requestUrl;
    this.correlationId = details.correlationId;
  }
}
