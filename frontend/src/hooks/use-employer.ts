import { useQuery } from '@tanstack/react-query';

import { listEmployerApplications } from '../api/employer';
import { useAuth } from './use-auth';

export const EMPLOYER_APPLICATIONS_QUERY_KEY = 'employer-applications';

export function useEmployerApplications() {
  const { isEmployer } = useAuth();
  return useQuery({
    queryKey: [EMPLOYER_APPLICATIONS_QUERY_KEY],
    queryFn: ({ signal }) => listEmployerApplications(signal),
    enabled: isEmployer,
    staleTime: 15_000,
  });
}
