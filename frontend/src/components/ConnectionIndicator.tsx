import type { StreamStatus } from '../hooks/use-vacancy-stream';

const STATUS_LABEL: Record<StreamStatus, string> = {
  connecting: 'Connecting…',
  connected: 'Live',
  reconnecting: 'Reconnecting…',
  disconnected: 'Offline',
};

export function ConnectionIndicator({ status }: { status: StreamStatus }) {
  return (
    <span
      className={`conn conn--${status}`}
      role="status"
      aria-live="polite"
      title={`Live updates: ${STATUS_LABEL[status]}`}
    >
      <span className="conn__dot" aria-hidden="true" />
      {STATUS_LABEL[status]}
    </span>
  );
}
