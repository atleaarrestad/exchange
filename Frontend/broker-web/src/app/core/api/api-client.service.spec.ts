import { ApiClient, CONTENT_TYPE, HEADER_NAME } from '@exchange/shared-api-client';

describe('ApiClient', () => {
  function createJsonResponse(body: unknown, status = 200): Response {
    return new Response(JSON.stringify(body), {
      status,
      headers: {
        [HEADER_NAME.ContentType]: CONTENT_TYPE.ApplicationJson
      }
    });
  }

  it('includes auth header only for same-origin requests by default', async () => {
    const seenRequests: Request[] = [];

    spyOn(window, 'fetch').and.callFake(async (input: RequestInfo | URL, init?: RequestInit) => {
      const request = input instanceof Request ? input : new Request(input, init);
      seenRequests.push(request);
      return createJsonResponse({ ok: true });
    });

    const client = new ApiClient({
      config: { baseUrl: 'https://api.example.com' },
      authTokenProvider: () => 'secret-token'
    });

    await client.get('/same-origin');
    await client.get('https://other.example.com/cross-origin');

    expect(seenRequests[0].headers.get(HEADER_NAME.Authorization)).toBe('Bearer secret-token');
    expect(seenRequests[1].headers.has(HEADER_NAME.Authorization)).toBeFalse();
  });

  it('retries transient transport errors for GET requests', async () => {
    let attempts = 0;

    spyOn(window, 'fetch').and.callFake(async () => {
      attempts += 1;
      if (attempts === 1) {
        throw new TypeError('Simulated network failure');
      }

      return createJsonResponse({ ok: true });
    });

    const client = new ApiClient({
      config: {
        baseUrl: 'https://api.example.com',
        retryPolicy: { maxAttempts: 2, baseDelayMs: 1, maxDelayMs: 1, jitterFactor: 0 }
      }
    });

    const response = await client.get<{ ok: boolean }>('/retry');

    expect(response.ok).toBeTrue();
    expect(attempts).toBe(2);
  });
});
