import { AlertIcon, InboxIcon } from './icons';

export function TableSkeleton({ rows = 8, columns = 10 }: { rows?: number; columns?: number }) {
  return (
    <tbody>
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <tr className="skeleton-row" key={rowIndex}>
          {Array.from({ length: columns }).map((__, cellIndex) => (
            <td key={cellIndex}>
              <span
                className="skeleton-bar"
                style={{ width: `${40 + ((rowIndex * 7 + cellIndex * 11) % 50)}%` }}
              />
            </td>
          ))}
        </tr>
      ))}
    </tbody>
  );
}

export function EmptyState() {
  return (
    <div className="state-panel">
      <span className="state-panel__icon">
        <InboxIcon />
      </span>
      <p className="state-panel__title">No vacancies match your filters</p>
      <p className="state-panel__text">
        Try widening your search or clearing a filter. New matches appear here automatically as they
        arrive.
      </p>
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="state-panel">
      <span className="state-panel__icon state-panel__icon--error">
        <AlertIcon />
      </span>
      <p className="state-panel__title">Couldn’t load vacancies</p>
      <p className="state-panel__text">{message}</p>
      <button type="button" className="button-primary" onClick={onRetry}>
        Try again
      </button>
    </div>
  );
}
