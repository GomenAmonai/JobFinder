import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { listApplications } from '../api/applications';
import { useAuth } from './use-auth';

export const APPLICATIONS_QUERY_KEY = 'applications';

export function useApplications() {
  const { isAuthenticated } = useAuth();
  return useQuery({
    queryKey: [APPLICATIONS_QUERY_KEY],
    queryFn: ({ signal }) => listApplications(signal),
    enabled: isAuthenticated,
    staleTime: 15_000,
  });
}

// Vacancy ids the current user already applied to — lets the table mark rows as applied.
export function useAppliedVacancyIds(): Set<number> {
  const { data } = useApplications();
  return useMemo(() => new Set((data ?? []).map((application) => application.vacancy.id)), [data]);
}
