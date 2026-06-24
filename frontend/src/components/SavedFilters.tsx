import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

import { createFilter, deleteFilter } from '../api/filters';
import { SAVED_FILTERS_QUERY_KEY, useSavedFilters } from '../hooks/use-saved-filters';
import { useToasts } from '../hooks/use-toasts';
import { BookmarkIcon, CloseIcon } from './icons';

import type { FilterState } from './FilterBar';
import type { SavedFilterDto } from '../types/filters';

interface SavedFiltersProps {
  current: FilterState;
  onApply: (filter: FilterState) => void;
}

function toFilterState(filter: SavedFilterDto): FilterState {
  return {
    market: filter.market ?? undefined,
    level: filter.level ?? undefined,
    stack: filter.stack ?? undefined,
    q: filter.q ?? undefined,
  };
}

export function SavedFilters({ current, onApply }: SavedFiltersProps) {
  const { data, isLoading } = useSavedFilters();
  const queryClient = useQueryClient();
  const { pushToast } = useToasts();
  const [name, setName] = useState('');

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [SAVED_FILTERS_QUERY_KEY] });

  const saveMutation = useMutation({
    mutationFn: () =>
      createFilter({
        name: name.trim(),
        market: current.market,
        level: current.level,
        stack: current.stack,
        q: current.q,
      }),
    onSuccess: () => {
      void invalidate();
      setName('');
      pushToast({ variant: 'updated', title: 'Filter saved' });
    },
    onError: () => pushToast({ variant: 'error', title: 'Could not save filter' }),
  });

  const removeMutation = useMutation({
    mutationFn: (id: number) => deleteFilter(id),
    onSuccess: () => void invalidate(),
  });

  const hasActive = Boolean(current.market || current.level || current.stack || current.q);
  const filters = data ?? [];

  return (
    <section className="card saved-filters" aria-label="Saved filters">
      <div className="saved-filters__head">
        <span className="field__label">
          <BookmarkIcon /> Saved filters
        </span>
      </div>

      {isLoading ? (
        <span className="muted-text">Loading…</span>
      ) : filters.length > 0 ? (
        <div className="chips">
          {filters.map((filter) => (
            <span className="saved-chip" key={filter.id}>
              <button type="button" className="saved-chip__apply" onClick={() => onApply(toFilterState(filter))}>
                {filter.name}
              </button>
              <button
                type="button"
                className="saved-chip__remove"
                aria-label={`Delete ${filter.name}`}
                onClick={() => removeMutation.mutate(filter.id)}
              >
                <CloseIcon size={11} />
              </button>
            </span>
          ))}
        </div>
      ) : (
        <span className="muted-text">No saved filters yet — set some filters and save them.</span>
      )}

      <form
        className="saved-filters__save"
        onSubmit={(event) => {
          event.preventDefault();
          if (name.trim() && hasActive) saveMutation.mutate();
        }}
      >
        <input
          className="text-input"
          placeholder="Name this search…"
          value={name}
          maxLength={100}
          onChange={(event) => setName(event.target.value)}
        />
        <button
          type="submit"
          className="page-button"
          disabled={!name.trim() || !hasActive || saveMutation.isPending}
          title={hasActive ? 'Save the current filters' : 'Set a filter first'}
        >
          Save current
        </button>
      </form>
    </section>
  );
}
