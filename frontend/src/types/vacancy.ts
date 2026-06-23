export interface VacancyDto {
  id: number;
  source: string;
  externalId: string;
  title: string;
  company: string | null;
  market: string | null;
  level: string | null;
  stack: string | null;
  location: string | null;
  salaryRaw: string | null;
  skills: string | null;
  url: string | null;
  publishedAt: string | null;
  firstSeen: string;
  lastSeen: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export type VacancyChangeOutcome = 'Inserted' | 'Updated';

export interface VacancyChangedPayload {
  source: string;
  externalId: string;
  title: string;
  company: string | null;
  market: string | null;
  level: string | null;
  stack: string | null;
  url: string | null;
  publishedAt: string | null;
  outcome: VacancyChangeOutcome;
}

export interface VacancyFilters {
  market?: string;
  level?: string;
  stack?: string;
  q?: string;
  page: number;
  pageSize: number;
}
