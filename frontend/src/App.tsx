import { useEffect, useMemo, useRef, useState } from 'react';

import { VacanciesApiError } from './api/vacancies';
import { FilterBar } from './components/FilterBar';
import { Header } from './components/Header';
import { Pagination } from './components/Pagination';
import { EmptyState, ErrorState } from './components/StatePanels';
import { SpinnerIcon } from './components/icons';
import { ToastRegion } from './components/ToastRegion';
import { VacancyTable } from './components/VacancyTable';
import { useToasts } from './hooks/use-toasts';
import { useVacancies } from './hooks/use-vacancies';
import { useVacancyStream } from './hooks/use-vacancy-stream';

import type { FilterState } from './components/FilterBar';
import type { VacancyFilters } from './types/vacancy';

const PAGE_SIZE = 20;

function toErrorMessage(error: unknown): string {
  if (error instanceof VacanciesApiError) {
    return `The API responded with ${error.status}. Make sure the backend is running.`;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return 'An unexpected error occurred while reaching the API.';
}

export function App() {
  const connectionStatus = useVacancyStream();
  const { pushToast } = useToasts();

  const [filters, setFilters] = useState<FilterState>({});
  const [page, setPage] = useState(1);

  const query: VacancyFilters = useMemo(
    () => ({ ...filters, page, pageSize: PAGE_SIZE }),
    [filters, page],
  );

  const { data, isLoading, isError, error, isFetching, refetch } = useVacancies(query);

  const handleFiltersChange = (next: FilterState) => {
    setFilters(next);
    setPage(1);
  };

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const totalPages = data?.totalPages ?? 0;
  const isInitialLoading = isLoading && !data;
  const isRefreshing = isFetching && !isInitialLoading;
  const isEmpty = !isInitialLoading && !isError && items.length === 0;
  // Show the full error panel only when there's nothing to fall back to; if a
  // background refetch (e.g. a live update) fails while data is shown, keep the
  // stale table and surface the failure as a transient toast instead.
  const hasShownData = items.length > 0;
  const showErrorPanel = isError && !hasShownData;

  const lastToastedError = useRef<unknown>(null);
  useEffect(() => {
    if (isError && hasShownData && error !== lastToastedError.current) {
      lastToastedError.current = error;
      pushToast({ variant: 'error', title: 'Refresh failed', description: toErrorMessage(error) });
    }
    if (!isError) lastToastedError.current = null;
  }, [isError, hasShownData, error, pushToast]);

  return (
    <div className="app-shell">
      <Header connectionStatus={connectionStatus} />

      <main className="app-main">
        <FilterBar value={filters} onChange={handleFiltersChange} />

        <div className="results-bar reveal">
          <span className="results-bar__count">
            {isInitialLoading ? (
              'Loading vacancies…'
            ) : (
              <>
                <strong>{total.toLocaleString()}</strong> {total === 1 ? 'vacancy' : 'vacancies'}
              </>
            )}
          </span>
          {isRefreshing && (
            <span className="results-bar__refreshing">
              <SpinnerIcon />
              Updating
            </span>
          )}
        </div>

        {showErrorPanel ? (
          <section className="card reveal">
            <ErrorState message={toErrorMessage(error)} onRetry={() => void refetch()} />
          </section>
        ) : isEmpty ? (
          <section className="card reveal">
            <EmptyState />
          </section>
        ) : (
          <>
            <section className="card reveal">
              <VacancyTable vacancies={items} isLoading={isInitialLoading} />
            </section>
            {!isInitialLoading && (
              <Pagination
                page={page}
                totalPages={totalPages}
                total={total}
                onPageChange={setPage}
              />
            )}
          </>
        )}
      </main>

      <footer className="footer">
        JobRadar · aggregating remote .NET &amp; backend roles
      </footer>

      <ToastRegion />
    </div>
  );
}
