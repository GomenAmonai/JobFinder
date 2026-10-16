export interface FilterOption {
  label: string;
  value: string;
}

// Filter VALUES must exactly equal the tokens the backend stores (it filters on
// strict string equality and normalizes locations to these market tokens, some
// Cyrillic). Only the LABELS are localized/prettified for display.
export const MARKET_OPTIONS: FilterOption[] = [
  { label: 'Worldwide', value: 'Worldwide' },
  { label: 'Europe', value: 'Европа' },
  { label: 'US', value: 'США' },
  { label: 'Canada', value: 'Канада' },
  { label: 'Russia', value: 'Россия' },
  { label: 'CIS', value: 'СНГ' },
  { label: 'Asia', value: 'Азия' },
  { label: 'Japan', value: 'Япония' },
];

const MARKET_LABELS = new Map(MARKET_OPTIONS.map((option) => [option.value, option.label]));

export function marketLabel(market: string): string {
  return MARKET_LABELS.get(market) ?? market;
}
