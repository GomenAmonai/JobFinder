export const APPLICATION_STATUSES = [
  'Submitted',
  'UnderReview',
  'InterviewScheduled',
  'OfferExtended',
  'Rejected',
  'Withdrawn',
] as const;

export type ApplicationStatus = (typeof APPLICATION_STATUSES)[number];

export const STATUS_LABELS: Record<ApplicationStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  InterviewScheduled: 'Interview',
  OfferExtended: 'Offer',
  Rejected: 'Rejected',
  Withdrawn: 'Withdrawn',
};

const ACTIVE_ORDER: ApplicationStatus[] = [
  'Submitted',
  'UnderReview',
  'InterviewScheduled',
  'OfferExtended',
];

export function isTerminal(status: ApplicationStatus): boolean {
  return status === 'Rejected' || status === 'Withdrawn';
}

// Mirrors the server state-machine (ApplicationStatusTransitions): among active
// stages only forward; into a terminal state from any active one; out of terminal
// nowhere. The server is still the authority — this only shapes the UI options.
export function allowedTransitions(from: ApplicationStatus): ApplicationStatus[] {
  if (isTerminal(from)) return [];
  const fromIndex = ACTIVE_ORDER.indexOf(from);
  const forward = ACTIVE_ORDER.slice(fromIndex + 1);
  return [...forward, 'Rejected', 'Withdrawn'];
}

export interface ApplicationVacancyRef {
  id: number;
  title: string;
  company: string | null;
  url: string | null;
  market: string | null;
  level: string | null;
}

export interface ApplicationDto {
  id: number;
  status: ApplicationStatus;
  coverLetter: string | null;
  version: string;
  createdAt: string;
  updatedAt: string;
  vacancy: ApplicationVacancyRef;
}

export interface EmployerApplicationDto {
  id: number;
  status: ApplicationStatus;
  coverLetter: string | null;
  version: string;
  createdAt: string;
  updatedAt: string;
  candidateEmail: string;
  candidateDisplayName: string | null;
  vacancy: ApplicationVacancyRef;
}
