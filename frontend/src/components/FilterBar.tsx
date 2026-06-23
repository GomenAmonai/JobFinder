import { useEffect, useState } from 'react';

import { useDebouncedValue } from '../hooks/use-debounced-value';
import { SearchIcon } from './icons';

interface FilterOption {
  label: string;
  value: string;
}

// Filter VALUES must exactly equal the tokens the backend stores (it filters on
// strict string equality and normalizes locations to these market tokens, some
// Cyrillic). Only the LABELS are localized/prettified for display.
const MARKET_OPTIONS: FilterOption[] = [
  { label: 'Worldwide', value: 'Worldwide' },
  { label: 'Europe', value: 'Европа' },
  { label: 'US', value: 'США' },
  { label: 'Canada', value: 'Канада' },
  { label: 'Russia', value: 'Россия' },
  { label: 'CIS', value: 'СНГ' },
  { label: 'Asia', value: 'Азия' },
  { label: 'Japan', value: 'Япония' },
];

const LEVEL_OPTIONS: FilterOption[] = [
  { label: 'Junior', value: 'junior' },
  { label: 'Middle', value: 'middle' },
  { label: 'Senior+', value: 'senior+' },
  { label: 'Mid / Unknown', value: 'mid/unknown' },
];

const STACK_OPTIONS: FilterOption[] = [
  { label: 'C# / .NET', value: 'C#/.NET' },
  { label: 'Backend', value: 'backend' },
];

export interface FilterState {
  market?: string;
  level?: string;
  stack?: string;
  q?: string;
}

interface FilterBarProps {
  value: FilterState;
  onChange: (next: FilterState) => void;
  searchDebounceMs?: number;
}

export function FilterBar({ value, onChange, searchDebounceMs = 300 }: FilterBarProps) {
  const [searchText, setSearchText] = useState(value.q ?? '');
  const debouncedSearch = useDebouncedValue(searchText, searchDebounceMs);

  useEffect(() => {
    const next = debouncedSearch.trim();
    const current = value.q ?? '';
    if (next === current) return;
    onChange({ ...value, q: next || undefined });
    // Depends on the debounced term only — `value`/`onChange` are read fresh on
    // each run; including them would re-fire on every keystroke and defeat debouncing.
  }, [debouncedSearch]);

  const toggle = (key: keyof FilterState, optionValue: string) => {
    const isActive = value[key] === optionValue;
    onChange({ ...value, [key]: isActive ? undefined : optionValue });
  };

  const hasActiveFilters =
    Boolean(value.market) || Boolean(value.level) || Boolean(value.stack) || Boolean(value.q);

  const reset = () => {
    setSearchText('');
    onChange({});
  };

  return (
    <section className="card filters" aria-label="Filters">
      <div className="filters__row">
        <div className="field filters__search">
          <label className="field__label" htmlFor="vacancy-search">
            Search
          </label>
          <div className="search-input">
            <SearchIcon className="search-input__icon" />
            <input
              id="vacancy-search"
              type="search"
              inputMode="search"
              placeholder="Title, company, skills…"
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
            />
          </div>
        </div>

        {hasActiveFilters && (
          <button type="button" className="filters__reset" onClick={reset}>
            Clear filters
          </button>
        )}
      </div>

      <div className="filters__row">
        <ChipGroup
          label="Market"
          options={MARKET_OPTIONS}
          selected={value.market}
          onToggle={(optionValue) => toggle('market', optionValue)}
        />
        <ChipGroup
          label="Level"
          options={LEVEL_OPTIONS}
          selected={value.level}
          onToggle={(optionValue) => toggle('level', optionValue)}
        />
        <ChipGroup
          label="Stack"
          options={STACK_OPTIONS}
          selected={value.stack}
          onToggle={(optionValue) => toggle('stack', optionValue)}
        />
      </div>
    </section>
  );
}

interface ChipGroupProps {
  label: string;
  options: FilterOption[];
  selected?: string;
  onToggle: (value: string) => void;
}

function ChipGroup({ label, options, selected, onToggle }: ChipGroupProps) {
  return (
    <div className="field" role="group" aria-label={label}>
      <span className="field__label">{label}</span>
      <div className="chips">
        {options.map((option) => (
          <button
            key={option.value}
            type="button"
            className="chip"
            aria-pressed={selected === option.value}
            onClick={() => onToggle(option.value)}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  );
}
