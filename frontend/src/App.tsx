import { useEffect, useMemo, useRef, useState } from 'react';

import { ApiError } from './api/http';
import { ApplicationsBoard } from './components/ApplicationsBoard';
import { ApplyDialog } from './components/ApplyDialog';
import { AuthDialog } from './components/AuthDialog';
import { EmployerPanel } from './components/EmployerPanel';
import { FilterBar } from './components/FilterBar';
import { Header } from './components/Header';
import { Pagination } from './components/Pagination';
import { SavedFilters } from './components/SavedFilters';
import { EmptyState, ErrorState } from './components/StatePanels';
import { SpinnerIcon } from './components/icons';
import { ToastRegion } from './components/ToastRegion';
import { VacancyTable } from './components/VacancyTable';
import { useAppliedVacancyIds } from './hooks/use-applications';
import { useAuth } from './hooks/use-auth';
import { useToasts } from './hooks/use-toasts';
import { useVacancies } from './hooks/use-vacancies';
import { useVacancyStream } from './hooks/use-vacancy-stream';

import type { AppView } from './components/Header';
import type { FilterState } from './components/FilterBar';
import type { VacancyDto, VacancyFilters } from './types/vacancy';

const PAGE_SIZE = 20;

function toErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return `The API responded with ${error.status}. Make sure the backend is running.`;
  }
  if (error instanceof Error) return error.message;
  return 'An unexpected error occurred while reaching the API.';
}

export function App() {
  const connectionStatus = useVacancyStream();
  const { pushToast } = useToasts();
  const { isAuthenticated, isEmployer } = useAuth();

  const [view, setView] = useState<AppView>('browse');
  const [authOpen, setAuthOpen] = useState(false);
  const [applyTarget, setApplyTarget] = useState<VacancyDto | null>(null);
  const [filters, setFilters] = useState<FilterState>({});
  const [page, setPage] = useState(1);

  // Keep the view consistent with the session: a logged-out user (or a candidate)
  // can't sit on an auth-only / employer-only tab.
  useEffect(() => {
    if (!isAuthenticated && view !== 'browse') setView('browse');
    else if (!isEmployer && view === 'employer') setView('browse');
  }, [isAuthenticated, isEmployer, view]);

  const query: VacancyFilters = useMemo(
    () => ({ ...filters, page, pageSize: PAGE_SIZE }),
    [filters, page],
  );

  const { data, isLoading, isError, error, isFetching, refetch } = useVacancies(query);
  const appliedVacancyIds = useAppliedVacancyIds();

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

  const handleApply = (vacancy: VacancyDto) => {
    if (!isAuthenticated) {
      setAuthOpen(true);
      return;
    }
    setApplyTarget(vacancy);
  };

  return (
    <div className="app-shell">
      <Header
        connectionStatus={connectionStatus}
        view={view}
        onViewChange={setView}
        onSignIn={() => setAuthOpen(true)}
      />

      <main className="app-main">
        {view === 'browse' && (
          <>
            <FilterBar value={filters} onChange={handleFiltersChange} />
            {isAuthenticated && <SavedFilters current={filters} onApply={handleFiltersChange} />}

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
                  <VacancyTable
                    vacancies={items}
                    isLoading={isInitialLoading}
                    appliedVacancyIds={appliedVacancyIds}
                    onApply={isAuthenticated ? handleApply : undefined}
                  />
                </section>
                {!isInitialLoading && (
                  <Pagination page={page} totalPages={totalPages} total={total} onPageChange={setPage} />
                )}
              </>
            )}
          </>
        )}

        {view === 'applications' && (
          <section className="card reveal section-pad">
            <h1 className="section-title">My applications</h1>
            <ApplicationsBoard />
          </section>
        )}

        {view === 'employer' && (
          <div className="reveal">
            <EmployerPanel />
          </div>
        )}
      </main>

      <footer className="footer">JobRadar · aggregating remote .NET &amp; backend roles</footer>

      {authOpen && <AuthDialog onClose={() => setAuthOpen(false)} />}
      {applyTarget && <ApplyDialog vacancy={applyTarget} onClose={() => setApplyTarget(null)} />}

      <ToastRegion />
    </div>
  );
}
