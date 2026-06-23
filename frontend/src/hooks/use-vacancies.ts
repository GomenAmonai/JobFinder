import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { fetchVacancies } from '../api/vacancies';

import type { PagedResult, VacancyDto, VacancyFilters } from '../types/vacancy';

export const VACANCIES_QUERY_KEY = 'vacancies';

export function useVacancies(filters: VacancyFilters) {
  return useQuery<PagedResult<VacancyDto>>({
    queryKey: [VACANCIES_QUERY_KEY, filters],
    queryFn: ({ signal }) => fetchVacancies(filters, signal),
    placeholderData: keepPreviousData,
    staleTime: 15_000,
  });
}
