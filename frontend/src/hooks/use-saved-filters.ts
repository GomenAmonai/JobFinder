import { useQuery } from '@tanstack/react-query';

import { listFilters } from '../api/filters';
import { useAuth } from './use-auth';

export const SAVED_FILTERS_QUERY_KEY = 'saved-filters';

export function useSavedFilters() {
  const { isAuthenticated } = useAuth();
  return useQuery({
    queryKey: [SAVED_FILTERS_QUERY_KEY],
    queryFn: ({ signal }) => listFilters(signal),
    enabled: isAuthenticated,
    staleTime: 30_000,
  });
}
