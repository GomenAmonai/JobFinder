import { STATUS_LABELS } from '../types/applications';

import type { ApplicationStatus } from '../types/applications';

const PILL_MODIFIER: Record<ApplicationStatus, string> = {
  Submitted: 'submitted',
  UnderReview: 'review',
  InterviewScheduled: 'interview',
  OfferExtended: 'offer',
  Rejected: 'rejected',
  Withdrawn: 'withdrawn',
};

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  return <span className={`status-pill status-pill--${PILL_MODIFIER[status]}`}>{STATUS_LABELS[status]}</span>;
}

interface StatusControlProps {
  status: ApplicationStatus;
  options: ApplicationStatus[];
  onSelect: (next: ApplicationStatus) => void;
  pending?: boolean;
}

export function StatusControl({ status, options, onSelect, pending }: StatusControlProps) {
  return (
    <div className="status-control">
      <StatusBadge status={status} />
      {options.length > 0 && (
        <select
          className="status-select"
          value=""
          disabled={pending}
          aria-label="Change status"
          onChange={(event) => {
            const next = event.target.value as ApplicationStatus;
            if (next) onSelect(next);
          }}
        >
          <option value="">Change…</option>
          {options.map((option) => (
            <option key={option} value={option}>
              {STATUS_LABELS[option]}
            </option>
          ))}
        </select>
      )}
    </div>
  );
}
