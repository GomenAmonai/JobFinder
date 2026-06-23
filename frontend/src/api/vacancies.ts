import { API_BASE_URL } from './config';

import type { PagedResult, VacancyDto, VacancyFilters } from '../types/vacancy';

export class VacanciesApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'VacanciesApiError';
    this.status = status;
  }
}

function buildQuery(filters: VacancyFilters): string {
  const params = new URLSearchParams();
  if (filters.market) params.set('market', filters.market);
  if (filters.level) params.set('level', filters.level);
  if (filters.stack) params.set('stack', filters.stack);
  if (filters.q) params.set('q', filters.q);
  params.set('page', String(filters.page));
  params.set('pageSize', String(filters.pageSize));
  return params.toString();
}

export async function fetchVacancies(
  filters: VacancyFilters,
  signal?: AbortSignal,
): Promise<PagedResult<VacancyDto>> {
  const query = buildQuery(filters);
  const response = await fetch(`${API_BASE_URL}/vacancies?${query}`, {
    headers: { Accept: 'application/json' },
    signal,
  });

  if (!response.ok) {
    throw new VacanciesApiError(
      response.status,
      `Request failed with status ${response.status} ${response.statusText}`.trim(),
    );
  }

  return (await response.json()) as PagedResult<VacancyDto>;
}
