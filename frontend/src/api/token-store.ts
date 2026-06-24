import type { TokenPair } from '../types/auth';

// JWT pair lives in localStorage so a refresh of the page keeps the session.
// A module-level store (not React state) lets the HTTP layer read/refresh tokens
// without prop-drilling; the auth context subscribes to reflect changes in the UI.
const STORAGE_KEY = 'jobradar.auth';

let current: TokenPair | null = read();
const listeners = new Set<() => void>();

function read(): TokenPair | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as TokenPair) : null;
  } catch {
    return null;
  }
}

function emit(): void {
  listeners.forEach((listener) => listener());
}

export function getTokens(): TokenPair | null {
  return current;
}

export function setTokens(tokens: TokenPair): void {
  current = tokens;
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));
  } catch {
    // storage unavailable (private mode) — session stays in-memory only
  }
  emit();
}

export function clearTokens(): void {
  current = null;
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // ignore
  }
  emit();
}

export function subscribeTokens(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
