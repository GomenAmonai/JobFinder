import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

import { changeEmployerApplicationStatus, postVacancy } from '../api/employer';
import { ApiError } from '../api/http';
import { formatPublishedDate } from '../api/format';
import { EMPLOYER_APPLICATIONS_QUERY_KEY, useEmployerApplications } from '../hooks/use-employer';
import { useToasts } from '../hooks/use-toasts';
import { VACANCIES_QUERY_KEY } from '../hooks/use-vacancies';
import { InboxIcon } from './icons';
import { StatusControl } from './StatusControl';

import { allowedTransitions } from '../types/applications';
import type { ChangeEvent } from 'react';
import type { ApplicationStatus, EmployerApplicationDto } from '../types/applications';
import type { PostVacancyInput } from '../api/employer';

const EMPTY_FORM: PostVacancyInput = { title: '', company: '', location: '', salaryRaw: '', skills: '', url: '' };

export function EmployerPanel() {
  const queryClient = useQueryClient();
  const { pushToast } = useToasts();
  const [form, setForm] = useState<PostVacancyInput>(EMPTY_FORM);

  const postMutation = useMutation({
    mutationFn: () =>
      postVacancy({
        title: form.title.trim(),
        company: form.company?.trim() || undefined,
        location: form.location?.trim() || undefined,
        salaryRaw: form.salaryRaw?.trim() || undefined,
        skills: form.skills?.trim() || undefined,
        url: form.url?.trim() || undefined,
      }),
    onSuccess: (vacancy) => {
      void queryClient.invalidateQueries({ queryKey: [VACANCIES_QUERY_KEY] });
      pushToast({ variant: 'inserted', title: 'Vacancy posted', description: vacancy.title });
      setForm(EMPTY_FORM);
    },
    onError: (error) => {
      const description =
        error instanceof ApiError && error.status === 400
          ? 'Check the form — title is required, URL must be http(s).'
          : 'Could not post the vacancy.';
      pushToast({ variant: 'error', title: 'Post failed', description });
    },
  });

  const set = (key: keyof PostVacancyInput) => (event: ChangeEvent<HTMLInputElement>) =>
    setForm((current) => ({ ...current, [key]: event.target.value }));

  return (
    <div className="employer">
      <section className="card employer__form-card">
        <h2 className="section-title">Post a vacancy</h2>
        <form
          className="form form--grid"
          onSubmit={(event) => {
            event.preventDefault();
            postMutation.mutate();
          }}
        >
          <label className="field form--grid__wide">
            <span className="field__label">Title *</span>
            <input className="text-input" required maxLength={500} value={form.title} onChange={set('title')} />
          </label>
          <label className="field">
            <span className="field__label">Company</span>
            <input className="text-input" maxLength={300} value={form.company} onChange={set('company')} />
          </label>
          <label className="field">
            <span className="field__label">Location</span>
            <input
              className="text-input"
              maxLength={200}
              placeholder="e.g. Worldwide, Berlin…"
              value={form.location}
              onChange={set('location')}
            />
          </label>
          <label className="field">
            <span className="field__label">Salary</span>
            <input
              className="text-input"
              maxLength={100}
              placeholder="$90k – $120k"
              value={form.salaryRaw}
              onChange={set('salaryRaw')}
            />
          </label>
          <label className="field">
            <span className="field__label">Apply URL</span>
            <input
              className="text-input"
              type="url"
              maxLength={1000}
              placeholder="https://…"
              value={form.url}
              onChange={set('url')}
            />
          </label>
          <label className="field form--grid__wide">
            <span className="field__label">Skills (comma-separated)</span>
            <input
              className="text-input"
              maxLength={500}
              placeholder="C#, ASP.NET, PostgreSQL"
              value={form.skills}
              onChange={set('skills')}
            />
          </label>
          <button type="submit" className="button-primary form--grid__wide" disabled={postMutation.isPending}>
            {postMutation.isPending ? 'Posting…' : 'Post vacancy'}
          </button>
        </form>
      </section>

      <section className="card employer__applications">
        <h2 className="section-title">Incoming applications</h2>
        <EmployerApplications />
      </section>
    </div>
  );
}

function EmployerApplications() {
  const { data, isLoading, isError } = useEmployerApplications();
  const queryClient = useQueryClient();
  const { pushToast } = useToasts();

  const statusMutation = useMutation({
    mutationFn: (input: { id: number; status: ApplicationStatus; version: string }) =>
      changeEmployerApplicationStatus(input.id, input.status, input.version),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [EMPLOYER_APPLICATIONS_QUERY_KEY] }),
    onError: (error) => {
      void queryClient.invalidateQueries({ queryKey: [EMPLOYER_APPLICATIONS_QUERY_KEY] });
      const description =
        error instanceof ApiError && error.status === 409
          ? 'It changed elsewhere — reloaded, try again.'
          : 'Could not update the status.';
      pushToast({ variant: 'error', title: 'Status not changed', description });
    },
  });

  if (isLoading) return <p className="muted-text">Loading applications…</p>;
  if (isError) return <p className="muted-text">Could not load applications.</p>;

  const applications = data ?? [];
  if (applications.length === 0) {
    return (
      <div className="state-panel">
        <span className="state-panel__icon">
          <InboxIcon />
        </span>
        <p className="state-panel__title">No applications yet</p>
        <p className="state-panel__text">
          Candidates who apply to your posted vacancies appear here. Move them through the stages —
          they get a live update.
        </p>
      </div>
    );
  }

  return (
    <ul className="app-list">
      {applications.map((application: EmployerApplicationDto) => (
        <li className="app-card" key={application.id}>
          <div className="app-card__main">
            <span className="app-card__title">{application.vacancy.title}</span>
            <div className="app-card__meta">
              <span className="cell-company">
                {application.candidateDisplayName ?? application.candidateEmail}
              </span>
              <a className="cell-mono" href={`mailto:${application.candidateEmail}`}>
                {application.candidateEmail}
              </a>
              <span className="cell-mono">applied {formatPublishedDate(application.createdAt)}</span>
            </div>
            {application.coverLetter && <p className="app-card__cover">{application.coverLetter}</p>}
          </div>
          <div className="app-card__actions">
            <StatusControl
              status={application.status}
              options={allowedTransitions(application.status).filter((status) => status !== 'Withdrawn')}
              pending={statusMutation.isPending}
              onSelect={(status) =>
                statusMutation.mutate({ id: application.id, status, version: application.version })
              }
            />
          </div>
        </li>
      ))}
    </ul>
  );
}
