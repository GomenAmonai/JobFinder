import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

import { applyToVacancy } from '../api/applications';
import { ApiError } from '../api/http';
import { APPLICATIONS_QUERY_KEY } from '../hooks/use-applications';
import { useToasts } from '../hooks/use-toasts';
import { Modal } from './Modal';

import type { VacancyDto } from '../types/vacancy';

export function ApplyDialog({ vacancy, onClose }: { vacancy: VacancyDto; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { pushToast } = useToasts();
  const [coverLetter, setCoverLetter] = useState('');

  const mutation = useMutation({
    mutationFn: () => applyToVacancy(vacancy.id, coverLetter.trim() || undefined),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });
      pushToast({ variant: 'inserted', title: 'Application sent', description: vacancy.title });
      onClose();
    },
    onError: (error) => {
      const description =
        error instanceof ApiError && error.status === 409
          ? 'You already applied to this vacancy.'
          : 'Could not send the application.';
      pushToast({ variant: 'error', title: 'Apply failed', description });
    },
  });

  return (
    <Modal title="Apply to vacancy" onClose={onClose}>
      <p className="muted-text">
        {vacancy.title}
        {vacancy.company ? ` · ${vacancy.company}` : ''}
      </p>
      <form
        className="form"
        onSubmit={(event) => {
          event.preventDefault();
          mutation.mutate();
        }}
      >
        <label className="field">
          <span className="field__label">Cover letter (optional)</span>
          <textarea
            className="text-input text-area"
            rows={6}
            maxLength={5000}
            value={coverLetter}
            onChange={(event) => setCoverLetter(event.target.value)}
            placeholder="A short note to the employer…"
          />
        </label>
        <button type="submit" className="button-primary" disabled={mutation.isPending}>
          {mutation.isPending ? 'Sending…' : 'Send application'}
        </button>
      </form>
    </Modal>
  );
}
