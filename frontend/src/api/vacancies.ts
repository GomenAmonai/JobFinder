import { request } from './http';

import type { PagedResult, VacancyDto, VacancyFilters } from '../types/vacancy';

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

export function fetchVacancies(
  filters: VacancyFilters,
  signal?: AbortSignal,
): Promise<PagedResult<VacancyDto>> {
  return request<PagedResult<VacancyDto>>(`/vacancies?${buildQuery(filters)}`, { signal });
}
