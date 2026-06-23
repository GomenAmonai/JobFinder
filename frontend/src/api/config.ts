const DEFAULT_API_URL = 'http://localhost:5088';

function resolveApiBaseUrl(): string {
  const fromEnv = import.meta.env.VITE_API_URL?.trim();
  const base = fromEnv && fromEnv.length > 0 ? fromEnv : DEFAULT_API_URL;
  return base.replace(/\/+$/, '');
}

export const API_BASE_URL = resolveApiBaseUrl();

export const VACANCIES_HUB_URL = `${API_BASE_URL}/hubs/vacancies`;
