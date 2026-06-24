import { Observable } from 'rxjs';
import { HTTP_METHOD, HttpMethod } from './api-client.constants';
import { ApiRequestOptions } from './api-client.models';
import { ApiClient } from './api-client.service';

export class ObservableApiClientAdapter {
  constructor(private readonly client: ApiClient) {}

  request<TResponse, TBody>(
    method: HttpMethod,
    path: string,
    options?: ApiRequestOptions<TBody>
  ): Observable<TResponse> {
    return this.createRequestObservable<TResponse, TBody>(method, path, options ?? {});
  }

  get<TResponse>(path: string, options?: Omit<ApiRequestOptions<never>, 'body'>): Observable<TResponse> {
    return this.createRequestObservable<TResponse, never>(HTTP_METHOD.Get, path, options ?? {});
  }

  post<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body'>
  ): Observable<TResponse> {
    return this.createRequestObservable<TResponse, TBody>(HTTP_METHOD.Post, path, { ...(options ?? {}), body });
  }

  put<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body'>
  ): Observable<TResponse> {
    return this.createRequestObservable<TResponse, TBody>(HTTP_METHOD.Put, path, { ...(options ?? {}), body });
  }

  patch<TResponse, TBody>(
    path: string,
    body: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'body'>
  ): Observable<TResponse> {
    return this.createRequestObservable<TResponse, TBody>(HTTP_METHOD.Patch, path, { ...(options ?? {}), body });
  }

  delete<TResponse>(path: string, options?: Omit<ApiRequestOptions<never>, 'body'>): Observable<TResponse> {
    return this.createRequestObservable<TResponse, never>(HTTP_METHOD.Delete, path, options ?? {});
  }

  private createRequestObservable<TResponse, TBody>(
    method: HttpMethod,
    path: string,
    options: ApiRequestOptions<TBody>
  ): Observable<TResponse> {
    return new Observable<TResponse>((subscriber) => {
      const unsubscribeController = new AbortController();
      const signal = combineAbortSignals(options.signal, unsubscribeController.signal);
      const requestOptions: ApiRequestOptions<TBody> = { ...options, signal };

      void this.client
        .request<TResponse, TBody>(method, path, requestOptions)
        .then((response) => {
          if (subscriber.closed) {
            return;
          }

          subscriber.next(response);
          subscriber.complete();
        })
        .catch((error: unknown) => {
          if (subscriber.closed) {
            return;
          }

          subscriber.error(error);
        });

      return () => {
        unsubscribeController.abort();
      };
    });
  }
}

function combineAbortSignals(externalSignal: AbortSignal | undefined, internalSignal: AbortSignal): AbortSignal {
  if (externalSignal === undefined) {
    return internalSignal;
  }

  if (typeof AbortSignal.any === 'function') {
    return AbortSignal.any([externalSignal, internalSignal]);
  }

  const combinedController = new AbortController();

  const abortCombinedSignal = (): void => {
    externalSignal.removeEventListener('abort', abortCombinedSignal);
    internalSignal.removeEventListener('abort', abortCombinedSignal);
    combinedController.abort();
  };

  if (externalSignal.aborted || internalSignal.aborted) {
    abortCombinedSignal();
    return combinedController.signal;
  }

  externalSignal.addEventListener('abort', abortCombinedSignal, { once: true });
  internalSignal.addEventListener('abort', abortCombinedSignal, { once: true });

  return combinedController.signal;
}
