const DEFAULT_API_URL = 'http://localhost:5088';

function resolveApiBaseUrl(): string {
  const fromEnv = import.meta.env.VITE_API_URL?.trim();
  const base = fromEnv && fromEnv.length > 0 ? fromEnv : DEFAULT_API_URL;
  return base.replace(/\/+$/, '');
}

// Demo mode swaps the network for an in-browser mock of the API (./demo/server).
// On explicitly via VITE_DEMO_MODE=true; also on for a production build with no
// VITE_API_URL — a static deploy (e.g. Vercel) without a backend would otherwise
// point at localhost and show nothing but errors.
function resolveDemoMode(): boolean {
  const flag = import.meta.env.VITE_DEMO_MODE?.trim().toLowerCase();
  if (flag === 'true') return true;
  if (flag === 'false') return false;
  return import.meta.env.PROD && !import.meta.env.VITE_API_URL?.trim();
}

export const API_BASE_URL = resolveApiBaseUrl();

export const VACANCIES_HUB_URL = `${API_BASE_URL}/hubs/vacancies`;

export const DEMO_MODE = resolveDemoMode();

export const REPOSITORY_URL = 'https://github.com/GomenAmonai/JobFinder';
