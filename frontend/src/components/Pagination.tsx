import { ChevronLeftIcon, ChevronRightIcon } from './icons';

interface PaginationProps {
  page: number;
  totalPages: number;
  total: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, total, onPageChange }: PaginationProps) {
  const safeTotalPages = Math.max(totalPages, 1);
  const canGoPrev = page > 1;
  const canGoNext = page < safeTotalPages;

  return (
    <nav className="card pagination" aria-label="Pagination">
      <span className="pagination__status">
        Page <strong>{page}</strong> of <strong>{safeTotalPages}</strong>
        {' · '}
        <strong>{total.toLocaleString()}</strong> {total === 1 ? 'result' : 'results'}
      </span>
      <div className="pagination__controls">
        <button
          type="button"
          className="page-button"
          onClick={() => onPageChange(page - 1)}
          disabled={!canGoPrev}
        >
          <ChevronLeftIcon />
          Prev
        </button>
        <button
          type="button"
          className="page-button"
          onClick={() => onPageChange(page + 1)}
          disabled={!canGoNext}
        >
          Next
          <ChevronRightIcon />
        </button>
      </div>
    </nav>
  );
}
