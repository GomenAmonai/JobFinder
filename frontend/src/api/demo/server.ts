import { allowedTransitions } from '../../types/applications';
import { INCOMING_VACANCIES, NATIVE_COMPANY, SEED_APPLICANTS, SEED_VACANCIES } from './fixtures';

import type {
  ApplicationDto,
  ApplicationStatus,
  ApplicationVacancyRef,
  EmployerApplicationDto,
} from '../../types/applications';
import type { AuthUser, TokenPair, UserRole } from '../../types/auth';
import type { SavedFilterDto } from '../../types/filters';
import type { VacancyChangedPayload, VacancyDto } from '../../types/vacancy';
import type { VacancySeed } from './fixtures';

// In-browser stand-in for the JobRadar API, used when the frontend is deployed
// without a backend (see DEMO_MODE in ../config). It mirrors the real contract —
// routes, status codes, optimistic-concurrency versions, the application state
// machine, role checks — so the UI code runs unchanged. State lives in
// localStorage, so a demo session survives a page reload.

const STORAGE_KEY = 'jobradar.demo.v1';
const SCHEMA_VERSION = 1;
const LATENCY_MS = { min: 120, max: 320 };
const INGEST_FIRST_DELAY_MS = 8_000;
const INGEST_INTERVAL_MS = 25_000;

export const DEMO_PASSWORD = 'demo-password';
export const DEMO_ACCOUNTS: Record<UserRole, string> = {
  Candidate: 'candidate@demo.jobradar.dev',
  Employer: 'employer@demo.jobradar.dev',
};

interface DemoUser {
  id: string;
  email: string;
  password: string;
  role: UserRole;
  displayName: string | null;
}

interface DemoVacancy extends VacancyDto {
  ownerId: string | null;
}

interface DemoApplication {
  id: number;
  userId: string;
  vacancyId: number;
  status: ApplicationStatus;
  coverLetter: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

interface DemoFilter {
  id: number;
  userId: string;
  name: string;
  market: string | null;
  level: string | null;
  stack: string | null;
  q: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

interface DemoState {
  schema: number;
  users: DemoUser[];
  vacancies: DemoVacancy[];
  applications: DemoApplication[];
  filters: DemoFilter[];
  accessTokens: Record<string, string>;
  refreshTokens: Record<string, string>;
  nextId: { vacancy: number; application: number; filter: number; user: number };
  ingestCursor: number;
}

// ---------- persistence ----------

let state: DemoState | null = null;

function hoursAgo(hours: number): string {
  return new Date(Date.now() - hours * 3_600_000).toISOString();
}

function seed(): DemoState {
  const candidate: DemoUser = {
    id: 'u-candidate',
    email: DEMO_ACCOUNTS.Candidate,
    password: DEMO_PASSWORD,
    role: 'Candidate',
    displayName: 'Demo Candidate',
  };
  const employer: DemoUser = {
    id: 'u-employer',
    email: DEMO_ACCOUNTS.Employer,
    password: DEMO_PASSWORD,
    role: 'Employer',
    displayName: NATIVE_COMPANY,
  };
  const applicants: DemoUser[] = SEED_APPLICANTS.map((applicant, index) => ({
    id: `u-applicant-${index + 1}`,
    email: applicant.email,
    password: crypto.randomUUID(),
    role: 'Candidate',
    displayName: applicant.displayName,
  }));

  // Spread publish dates over the last ~10 days, newest first.
  const vacancies: DemoVacancy[] = SEED_VACANCIES.map((item, index) =>
    toVacancy(item, index + 1, hoursAgo(3 + index * 7.5), item.source === 'JobRadar' ? employer.id : null),
  );

  const native = vacancies.filter((vacancy) => vacancy.ownerId === employer.id);
  const aggregated = vacancies.filter((vacancy) => vacancy.ownerId === null);

  let applicationId = 0;
  const application = (
    userId: string,
    vacancyId: number,
    status: ApplicationStatus,
    ageHours: number,
    coverLetter: string | null = null,
  ): DemoApplication => ({
    id: ++applicationId,
    userId,
    vacancyId,
    status,
    coverLetter,
    version: 1,
    createdAt: hoursAgo(ageHours),
    updatedAt: hoursAgo(Math.max(ageHours - 20, 1)),
  });

  const applications: DemoApplication[] = [
    // The demo candidate's own pipeline: one native vacancy, two aggregated (personal tracker).
    application(candidate.id, native[1].id, 'Submitted', 30, 'Five years of C#; built a Kafka-based ingestion pipeline.'),
    application(candidate.id, aggregated[0].id, 'InterviewScheduled', 120),
    application(candidate.id, aggregated[2].id, 'UnderReview', 70),
    application(candidate.id, aggregated[6].id, 'Rejected', 200),
    // Inbound applications for the demo employer.
    application(applicants[0].id, native[0].id, 'UnderReview', 50, 'Led the move from cron scrapers to Kafka consumers at my last job.'),
    application(applicants[1].id, native[0].id, 'Submitted', 12),
    application(applicants[2].id, native[1].id, 'InterviewScheduled', 90, 'Happy to share a SignalR side project.'),
    application(applicants[0].id, native[2].id, 'Submitted', 6),
  ];

  const filters: DemoFilter[] = [
    {
      id: 1,
      userId: candidate.id,
      name: 'Senior .NET · Europe',
      market: 'Европа',
      level: 'senior+',
      stack: 'C#/.NET',
      q: null,
      version: 1,
      createdAt: hoursAgo(240),
      updatedAt: hoursAgo(240),
    },
  ];

  return {
    schema: SCHEMA_VERSION,
    users: [candidate, employer, ...applicants],
    vacancies,
    applications,
    filters,
    accessTokens: {},
    refreshTokens: {},
    nextId: {
      vacancy: vacancies.length + 1,
      application: applicationId + 1,
      filter: 2,
      user: 1,
    },
    ingestCursor: 0,
  };
}

function load(): DemoState {
  if (state) return state;
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const parsed = raw ? (JSON.parse(raw) as DemoState) : null;
    state = parsed && parsed.schema === SCHEMA_VERSION ? parsed : seed();
  } catch {
    state = seed();
  }
  return state;
}

function save(): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    // storage unavailable (private mode) — the demo keeps working in memory
  }
}

export function resetDemoData(): void {
  state = seed();
  save();
}

// ---------- mapping ----------

function toVacancy(item: VacancySeed, id: number, publishedAt: string, ownerId: string | null): DemoVacancy {
  return {
    id,
    source: item.source,
    externalId: `${item.source.toLowerCase()}-${id}`,
    title: item.title,
    company: item.company,
    market: item.market,
    level: item.level,
    stack: item.stack,
    location: item.location,
    salaryRaw: item.salaryRaw,
    skills: item.skills,
    url: null,
    publishedAt,
    firstSeen: publishedAt,
    lastSeen: publishedAt,
    ownerId,
  };
}

function publicVacancy(vacancy: DemoVacancy): VacancyDto {
  const { ownerId: _ownerId, ...dto } = vacancy;
  return dto;
}

function vacancyRef(vacancyId: number): ApplicationVacancyRef {
  const vacancy = load().vacancies.find((v) => v.id === vacancyId);
  return {
    id: vacancyId,
    title: vacancy?.title ?? 'Removed vacancy',
    company: vacancy?.company ?? null,
    url: vacancy?.url ?? null,
    market: vacancy?.market ?? null,
    level: vacancy?.level ?? null,
  };
}

function applicationDto(application: DemoApplication): ApplicationDto {
  return {
    id: application.id,
    status: application.status,
    coverLetter: application.coverLetter,
    version: String(application.version),
    createdAt: application.createdAt,
    updatedAt: application.updatedAt,
    vacancy: vacancyRef(application.vacancyId),
  };
}

function employerApplicationDto(application: DemoApplication): EmployerApplicationDto {
  const candidate = load().users.find((user) => user.id === application.userId);
  return {
    ...applicationDto(application),
    candidateEmail: candidate?.email ?? 'unknown@example.com',
    candidateDisplayName: candidate?.displayName ?? null,
  };
}

function filterDto(filter: DemoFilter): SavedFilterDto {
  const { userId: _userId, version, ...rest } = filter;
  return { ...rest, version: String(version) };
}

function changedPayload(vacancy: DemoVacancy, outcome: VacancyChangedPayload['outcome']): VacancyChangedPayload {
  return {
    source: vacancy.source,
    externalId: vacancy.externalId,
    title: vacancy.title,
    company: vacancy.company,
    market: vacancy.market,
    level: vacancy.level,
    stack: vacancy.stack,
    url: vacancy.url,
    publishedAt: vacancy.publishedAt,
    outcome,
  };
}

// Simplified mirror of VacancyNormalization for employer-posted vacancies.
function normalizeMarket(location: string): string {
  const text = location.toLowerCase();
  if (/europe|\beu\b|germany|poland|spain|uk\b|netherlands|portugal/.test(text)) return 'Европа';
  if (/canada/.test(text)) return 'Канада';
  if (/\bus\b|usa|united states/.test(text)) return 'США';
  if (/russia|росси|рф/.test(text)) return 'Россия';
  if (/\bcis\b|снг|kazakhstan|armenia|georgia/.test(text)) return 'СНГ';
  if (/japan|tokyo/.test(text)) return 'Япония';
  if (/asia|apac|singapore/.test(text)) return 'Азия';
  return 'Worldwide';
}

function guessLevel(text: string): string {
  const value = text.toLowerCase();
  if (/senior|lead|staff|principal|architect/.test(value)) return 'senior+';
  if (/junior|intern|trainee/.test(value)) return 'junior';
  if (/middle|\bmid\b/.test(value)) return 'middle';
  return 'mid/unknown';
}

function detectStack(text: string): string {
  return /c#|\.net|asp\.net|blazor|ef core/i.test(text) ? 'C#/.NET' : 'backend';
}

// ---------- events (stand-in for the SignalR hub) ----------

export type DemoEvent =
  | { type: 'VacancyChanged'; payload: VacancyChangedPayload }
  | { type: 'MatchedVacancy'; userId: string; payload: VacancyChangedPayload }
  | { type: 'ApplicationStatusChanged'; userId: string; payload: ApplicationDto };

const listeners = new Set<(event: DemoEvent) => void>();
let ingestTimer: number | null = null;

function emit(event: DemoEvent): void {
  listeners.forEach((listener) => listener(event));
}

function matchesFilter(filter: DemoFilter, vacancy: DemoVacancy): boolean {
  if (filter.market && filter.market !== vacancy.market) return false;
  if (filter.level && filter.level !== vacancy.level) return false;
  if (filter.stack && filter.stack !== vacancy.stack) return false;
  if (filter.q && !searchText(vacancy).includes(filter.q.toLowerCase())) return false;
  return true;
}

// One tick of the simulated worker: insert the next "scraped" vacancy, or — once
// the pool is drained, and every third tick — re-see an existing one (Updated).
function ingestTick(): void {
  const db = load();
  const now = new Date().toISOString();
  const pool = INCOMING_VACANCIES;
  const shouldUpdate = db.ingestCursor >= pool.length || db.ingestCursor % 3 === 2;

  if (shouldUpdate) {
    const candidates = db.vacancies.filter((vacancy) => vacancy.ownerId === null);
    const vacancy = candidates[Math.floor(Math.random() * Math.min(candidates.length, 12))];
    if (!vacancy) return;
    vacancy.lastSeen = now;
    db.ingestCursor += 1;
    save();
    emit({ type: 'VacancyChanged', payload: changedPayload(vacancy, 'Updated') });
    return;
  }

  const vacancy = toVacancy(pool[db.ingestCursor], db.nextId.vacancy++, now, null);
  db.vacancies.push(vacancy);
  db.ingestCursor += 1;
  save();

  const payload = changedPayload(vacancy, 'Inserted');
  emit({ type: 'VacancyChanged', payload });
  const matchedUsers = new Set(db.filters.filter((f) => matchesFilter(f, vacancy)).map((f) => f.userId));
  matchedUsers.forEach((userId) => emit({ type: 'MatchedVacancy', userId, payload }));
}

export function subscribeDemoEvents(listener: (event: DemoEvent) => void): () => void {
  listeners.add(listener);
  if (ingestTimer === null) {
    const schedule = (delay: number) => {
      ingestTimer = window.setTimeout(() => {
        ingestTick();
        schedule(INGEST_INTERVAL_MS);
      }, delay);
    };
    schedule(INGEST_FIRST_DELAY_MS);
  }
  return () => {
    listeners.delete(listener);
    if (listeners.size === 0 && ingestTimer !== null) {
      window.clearTimeout(ingestTimer);
      ingestTimer = null;
    }
  };
}

// ---------- HTTP routing ----------

interface DemoResult {
  status: number;
  body?: unknown;
}

const ok = (body: unknown): DemoResult => ({ status: 200, body });
const created = (body: unknown): DemoResult => ({ status: 201, body });
const noContent: DemoResult = { status: 204 };
const notFound: DemoResult = { status: 404 };
const unauthorized: DemoResult = { status: 401 };
const forbidden: DemoResult = { status: 403 };
const conflict = (error: string): DemoResult => ({ status: 409, body: { error } });
const validation = (errors: Record<string, string[]>): DemoResult => ({
  status: 400,
  body: { title: 'One or more validation errors occurred.', status: 400, errors },
});

function issueTokens(userId: string): TokenPair {
  const db = load();
  const accessToken = `demo-access.${crypto.randomUUID()}`;
  const refreshToken = `demo-refresh.${crypto.randomUUID()}`;
  db.accessTokens[accessToken] = userId;
  db.refreshTokens[refreshToken] = userId;
  save();
  return {
    accessToken,
    refreshToken,
    accessTokenExpiresAt: new Date(Date.now() + 7 * 86_400_000).toISOString(),
  };
}

function searchText(vacancy: DemoVacancy): string {
  return `${vacancy.title} ${vacancy.company ?? ''} ${vacancy.skills ?? ''}`.toLowerCase();
}

function trimOrNull(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : null;
}

type Body = Record<string, unknown>;

function route(method: string, url: URL, body: Body, user: DemoUser | null): DemoResult {
  const db = load();
  const path = url.pathname.replace(/\/+$/, '') || '/';
  const segments = path.split('/').filter(Boolean);
  const idAt = (index: number) => Number.parseInt(segments[index] ?? '', 10);

  // ----- auth -----
  if (path === '/auth/register' && method === 'POST') {
    const email = trimOrNull(body.email)?.toLowerCase() ?? '';
    const password = typeof body.password === 'string' ? body.password : '';
    const errors: Record<string, string[]> = {};
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = ['A valid email is required.'];
    if (password.length < 8 || password.length > 128) errors.password = ['8–128 characters.'];
    if (Object.keys(errors).length > 0) return validation(errors);
    if (db.users.some((u) => u.email === email)) return conflict('DuplicateEmail');
    const role: UserRole = body.role === 'Employer' ? 'Employer' : 'Candidate';
    const newUser: DemoUser = {
      id: `u-${db.nextId.user++}-${crypto.randomUUID().slice(0, 8)}`,
      email,
      password,
      role,
      displayName: trimOrNull(body.displayName),
    };
    db.users.push(newUser);
    save();
    return ok(issueTokens(newUser.id));
  }

  if (path === '/auth/login' && method === 'POST') {
    const email = trimOrNull(body.email)?.toLowerCase() ?? '';
    const found = db.users.find((u) => u.email === email && u.password === body.password);
    return found ? ok(issueTokens(found.id)) : unauthorized;
  }

  if (path === '/auth/refresh' && method === 'POST') {
    const token = typeof body.refreshToken === 'string' ? body.refreshToken : '';
    const userId = db.refreshTokens[token];
    if (!userId) return unauthorized;
    delete db.refreshTokens[token]; // rotation: a refresh token is single-use
    return ok(issueTokens(userId));
  }

  if (path === '/auth/me' && method === 'GET') {
    if (!user) return unauthorized;
    const me: AuthUser = { id: user.id, email: user.email, role: user.role };
    return ok(me);
  }

  // ----- public vacancies -----
  if (path === '/vacancies' && method === 'GET') {
    const market = url.searchParams.get('market');
    const level = url.searchParams.get('level');
    const stack = url.searchParams.get('stack');
    const q = url.searchParams.get('q')?.trim().toLowerCase().slice(0, 100);
    const page = Math.max(1, Number(url.searchParams.get('page')) || 1);
    const pageSize = Math.min(100, Math.max(1, Number(url.searchParams.get('pageSize')) || 20));

    const matching = db.vacancies
      .filter((v) => (!market || v.market === market) && (!level || v.level === level) && (!stack || v.stack === stack))
      .filter((v) => !q || searchText(v).includes(q))
      .sort((a, b) => (b.publishedAt ?? '').localeCompare(a.publishedAt ?? '') || b.id - a.id);

    const total = matching.length;
    return ok({
      items: matching.slice((page - 1) * pageSize, page * pageSize).map(publicVacancy),
      page,
      pageSize,
      total,
      totalPages: Math.ceil(total / pageSize),
    });
  }

  // Everything below requires a session.
  if (!user) return unauthorized;

  // ----- saved filters -----
  if (segments[0] === 'me' && segments[1] === 'filters') {
    const mine = db.filters.filter((f) => f.userId === user.id);
    if (segments.length === 2 && method === 'GET') return ok(mine.map(filterDto));

    if (segments.length === 2 && method === 'POST') {
      const name = trimOrNull(body.name);
      if (!name) return validation({ name: ['Name is required.'] });
      const now = new Date().toISOString();
      const filter: DemoFilter = {
        id: db.nextId.filter++,
        userId: user.id,
        name,
        market: trimOrNull(body.market),
        level: trimOrNull(body.level),
        stack: trimOrNull(body.stack),
        q: trimOrNull(body.q),
        version: 1,
        createdAt: now,
        updatedAt: now,
      };
      db.filters.push(filter);
      save();
      return created(filterDto(filter));
    }

    const filter = mine.find((f) => f.id === idAt(2));
    if (!filter) return notFound;

    if (method === 'PUT') {
      if (String(filter.version) !== body.version) return conflict('version_conflict');
      filter.name = trimOrNull(body.name) ?? filter.name;
      filter.market = trimOrNull(body.market);
      filter.level = trimOrNull(body.level);
      filter.stack = trimOrNull(body.stack);
      filter.q = trimOrNull(body.q);
      filter.version += 1;
      filter.updatedAt = new Date().toISOString();
      save();
      return ok(filterDto(filter));
    }

    if (method === 'DELETE') {
      db.filters = db.filters.filter((f) => f !== filter);
      save();
      return noContent;
    }
  }

  // ----- applying -----
  if (segments[0] === 'vacancies' && segments[2] === 'applications' && method === 'POST') {
    const vacancy = db.vacancies.find((v) => v.id === idAt(1));
    if (!vacancy) return notFound;
    const coverLetter = trimOrNull(body.coverLetter);
    if (coverLetter && coverLetter.length > 5000) return validation({ coverLetter: ['At most 5000 characters.'] });
    if (db.applications.some((a) => a.userId === user.id && a.vacancyId === vacancy.id)) {
      return conflict('already_applied');
    }
    const now = new Date().toISOString();
    const application: DemoApplication = {
      id: db.nextId.application++,
      userId: user.id,
      vacancyId: vacancy.id,
      status: 'Submitted',
      coverLetter,
      version: 1,
      createdAt: now,
      updatedAt: now,
    };
    db.applications.push(application);
    save();
    return created(applicationDto(application));
  }

  // ----- candidate pipeline -----
  if (segments[0] === 'me' && segments[1] === 'applications') {
    const mine = db.applications.filter((a) => a.userId === user.id);
    if (segments.length === 2 && method === 'GET') {
      return ok([...mine].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)).map(applicationDto));
    }

    const application = mine.find((a) => a.id === idAt(2));
    if (!application) return notFound;

    if (segments.length === 3 && method === 'GET') return ok(applicationDto(application));
    if (segments[3] === 'status' && method === 'PATCH') return changeStatus(application, body);
    if (segments.length === 3 && method === 'DELETE') {
      db.applications = db.applications.filter((a) => a !== application);
      save();
      return noContent;
    }
  }

  // ----- employer side -----
  if (segments[0] === 'employer') {
    if (user.role !== 'Employer') return forbidden;

    if (path === '/employer/vacancies' && method === 'POST') {
      const title = trimOrNull(body.title);
      if (!title) return validation({ title: ['Title is required.'] });
      const location = trimOrNull(body.location) ?? 'Remote';
      const skills = trimOrNull(body.skills);
      const classifyText = `${title} ${skills ?? ''}`;
      const now = new Date().toISOString();
      const id = db.nextId.vacancy++;
      const vacancy: DemoVacancy = {
        ...toVacancy(
          {
            source: 'JobRadar',
            title,
            company: trimOrNull(body.company) ?? user.displayName ?? NATIVE_COMPANY,
            market: normalizeMarket(location),
            level: guessLevel(classifyText),
            stack: detectStack(classifyText),
            location,
            salaryRaw: trimOrNull(body.salaryRaw),
            skills: skills ?? '',
          },
          id,
          now,
          user.id,
        ),
        url: trimOrNull(body.url),
      };
      db.vacancies.push(vacancy);
      save();
      emit({ type: 'VacancyChanged', payload: changedPayload(vacancy, 'Inserted') });
      return created(publicVacancy(vacancy));
    }

    const ownVacancyIds = new Set(db.vacancies.filter((v) => v.ownerId === user.id).map((v) => v.id));
    const inbox = db.applications.filter((a) => ownVacancyIds.has(a.vacancyId));

    if (path === '/employer/applications' && method === 'GET') {
      return ok([...inbox].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).map(employerApplicationDto));
    }

    if (segments[1] === 'applications' && segments[3] === 'status' && method === 'PATCH') {
      const application = inbox.find((a) => a.id === idAt(2));
      if (!application) return notFound;
      const result = changeStatus(application, body);
      if (result.status === 200) {
        emit({ type: 'ApplicationStatusChanged', userId: application.userId, payload: applicationDto(application) });
      }
      return result;
    }
  }

  return notFound;
}

function changeStatus(application: DemoApplication, body: Body): DemoResult {
  if (String(application.version) !== body.version) return conflict('version_conflict');
  const next = body.status as ApplicationStatus;
  if (!allowedTransitions(application.status).includes(next)) {
    return { status: 422, body: { error: 'illegal_transition' } };
  }
  application.status = next;
  application.version += 1;
  application.updatedAt = new Date().toISOString();
  save();
  return ok(applicationDto(application));
}

// fetch()-compatible entry point used by the HTTP layer in demo mode.
export async function demoFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const delay = LATENCY_MS.min + Math.random() * (LATENCY_MS.max - LATENCY_MS.min);
  await new Promise((resolve) => setTimeout(resolve, delay));
  if (init.signal?.aborted) throw new DOMException('Aborted', 'AbortError');

  const db = load();
  const headers = new Headers(init.headers);
  const token = headers.get('Authorization')?.replace(/^Bearer\s+/i, '') ?? '';
  const userId = token ? db.accessTokens[token] : undefined;
  const user = userId ? db.users.find((u) => u.id === userId) ?? null : null;

  let body: Body = {};
  if (typeof init.body === 'string' && init.body.length > 0) {
    try {
      body = JSON.parse(init.body) as Body;
    } catch {
      return new Response(null, { status: 400 });
    }
  }

  const result = route((init.method ?? 'GET').toUpperCase(), new URL(path, 'https://demo.local'), body, user);
  const hasBody = result.body !== undefined && result.status !== 204;
  return new Response(hasBody ? JSON.stringify(result.body) : null, {
    status: result.status,
    headers: hasBody ? { 'Content-Type': 'application/json' } : undefined,
  });
}
