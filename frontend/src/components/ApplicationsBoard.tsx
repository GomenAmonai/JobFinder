import { useMutation, useQueryClient } from '@tanstack/react-query';

import { changeApplicationStatus, deleteApplication } from '../api/applications';
import { ApiError } from '../api/http';
import { formatPublishedDate } from '../api/format';
import { APPLICATIONS_QUERY_KEY, useApplications } from '../hooks/use-applications';
import { useToasts } from '../hooks/use-toasts';
import { LevelBadge, MarketBadge } from './Badges';
import { ExternalLinkIcon, InboxIcon, TrashIcon } from './icons';
import { StatusControl } from './StatusControl';

import { allowedTransitions } from '../types/applications';
import type { ApplicationDto, ApplicationStatus } from '../types/applications';

function statusError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 422) return 'The employer manages this vacancy’s stages — you can only withdraw.';
    if (error.status === 409) return 'It changed elsewhere — reloaded, try again.';
  }
  return 'Could not update the application.';
}

export function ApplicationsBoard() {
  const { data, isLoading, isError } = useApplications();
  const queryClient = useQueryClient();
  const { pushToast } = useToasts();

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });

  const statusMutation = useMutation({
    mutationFn: (input: { id: number; status: ApplicationStatus; version: string }) =>
      changeApplicationStatus(input.id, input.status, input.version),
    onSuccess: () => void invalidate(),
    onError: (error) => {
      void invalidate();
      pushToast({ variant: 'error', title: 'Status not changed', description: statusError(error) });
    },
  });

  const removeMutation = useMutation({
    mutationFn: (id: number) => deleteApplication(id),
    onSuccess: () => void invalidate(),
    onError: () =>
      pushToast({ variant: 'error', title: 'Could not remove', description: 'Try again.' }),
  });

  if (isLoading) return <p className="muted-text">Loading your applications…</p>;
  if (isError) return <p className="muted-text">Could not load your applications.</p>;

  const applications = data ?? [];
  if (applications.length === 0) {
    return (
      <div className="state-panel">
        <span className="state-panel__icon">
          <InboxIcon />
        </span>
        <p className="state-panel__title">No applications yet</p>
        <p className="state-panel__text">
          Apply to a vacancy from the Browse tab — it lands here so you can track its stage.
        </p>
      </div>
    );
  }

  return (
    <ul className="app-list">
      {applications.map((application: ApplicationDto) => (
        <li className="app-card" key={application.id}>
          <div className="app-card__main">
            {application.vacancy.url ? (
              <a
                className="app-card__title"
                href={application.vacancy.url}
                target="_blank"
                rel="noopener noreferrer"
              >
                {application.vacancy.title}
                <ExternalLinkIcon className="cell-title__ext" />
              </a>
            ) : (
              <span className="app-card__title">{application.vacancy.title}</span>
            )}
            <div className="app-card__meta">
              {application.vacancy.company && <span>{application.vacancy.company}</span>}
              <MarketBadge market={application.vacancy.market} />
              <LevelBadge level={application.vacancy.level} />
              <span className="cell-mono">applied {formatPublishedDate(application.createdAt)}</span>
            </div>
          </div>
          <div className="app-card__actions">
            <StatusControl
              status={application.status}
              options={allowedTransitions(application.status)}
              pending={statusMutation.isPending}
              onSelect={(status) =>
                statusMutation.mutate({ id: application.id, status, version: application.version })
              }
            />
            <button
              type="button"
              className="icon-button"
              aria-label="Remove from pipeline"
              disabled={removeMutation.isPending}
              onClick={() => removeMutation.mutate(application.id)}
            >
              <TrashIcon />
            </button>
          </div>
        </li>
      ))}
    </ul>
  );
}
