import { API_BASE_URL, DEMO_MODE } from './config';
import { demoFetch } from './demo/server';
import { clearTokens, getTokens, setTokens } from './token-store';

import type { TokenPair } from '../types/auth';

export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  auth?: boolean;
  signal?: AbortSignal;
}

// In demo mode every call is answered by the in-browser mock API instead of the network.
const transport = (path: string, init: RequestInit): Promise<Response> =>
  DEMO_MODE ? demoFetch(path, init) : fetch(`${API_BASE_URL}${path}`, init);

let refreshInFlight: Promise<boolean> | null = null;

// Single-flight refresh: concurrent 401s share one /auth/refresh round-trip.
// A failed refresh clears the session (the auth context reacts via subscription).
async function refreshTokens(): Promise<boolean> {
  const tokens = getTokens();
  if (!tokens?.refreshToken) return false;
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    try {
      const response = await transport('/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: tokens.refreshToken }),
      });
      if (!response.ok) {
        clearTokens();
        return false;
      }
      setTokens((await response.json()) as TokenPair);
      return true;
    } catch {
      clearTokens();
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, auth = false, signal } = options;

  const send = (): Promise<Response> => {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (auth) {
      const tokens = getTokens();
      if (tokens) headers.Authorization = `Bearer ${tokens.accessToken}`;
    }
    return transport(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
  };

  let response = await send();

  // Access token expired mid-session: refresh once and retry the original call.
  if (response.status === 401 && auth && getTokens()) {
    if (await refreshTokens()) response = await send();
  }

  if (!response.ok) {
    let parsed: unknown;
    try {
      parsed = await response.clone().json();
    } catch {
      parsed = undefined;
    }
    throw new ApiError(response.status, `Request to ${path} failed (${response.status})`, parsed);
  }

  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
