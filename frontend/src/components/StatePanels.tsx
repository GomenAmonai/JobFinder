export function TableSkeleton({ rows = 8, columns = 9 }: { rows?: number; columns?: number }) {
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
      <p className="state-panel__title">No vacancies match these filters</p>
      <p className="state-panel__text">
        Widen the search or clear a filter. New matches show up here as soon as they are ingested.
      </p>
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="state-panel state-panel--error">
      <p className="state-panel__title">Couldn’t load vacancies</p>
      <p className="state-panel__text">{message}</p>
      <button type="button" className="page-button" onClick={onRetry}>
        Try again
      </button>
    </div>
  );
}
