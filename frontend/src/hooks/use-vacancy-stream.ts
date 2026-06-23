import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { VACANCIES_HUB_URL } from '../api/config';
import { useToasts } from './use-toasts';
import { VACANCIES_QUERY_KEY } from './use-vacancies';

import type { VacancyChangedPayload } from '../types/vacancy';

export type StreamStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

const VACANCY_CHANGED_EVENT = 'VacancyChanged';
const INVALIDATE_DEBOUNCE_MS = 500;
const INITIAL_RETRY_BASE_MS = 1000;
const INITIAL_RETRY_MAX_MS = 30_000;

export function useVacancyStream(): StreamStatus {
  const queryClient = useQueryClient();
  const { pushToast } = useToasts();
  const [status, setStatus] = useState<StreamStatus>('connecting');

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(VACANCIES_HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    let isActive = true;
    let startPromise: Promise<void> | null = null;
    let retryHandle: number | null = null;
    let invalidateHandle: number | null = null;

    // Bursts from the ingestion worker can fire many VacancyChanged events in
    // quick succession; collapse them into one refetch of the active query.
    const scheduleInvalidate = () => {
      if (invalidateHandle !== null) return;
      invalidateHandle = window.setTimeout(() => {
        invalidateHandle = null;
        void queryClient.invalidateQueries({ queryKey: [VACANCIES_QUERY_KEY] });
      }, INVALIDATE_DEBOUNCE_MS);
    };

    connection.on(VACANCY_CHANGED_EVENT, (payload: VacancyChangedPayload) => {
      scheduleInvalidate();

      const description = [payload.company, payload.source].filter(Boolean).join(' · ');
      if (payload.outcome === 'Inserted') {
        pushToast({ variant: 'inserted', title: `New vacancy: ${payload.title}`, description });
      } else {
        pushToast({ variant: 'updated', title: `Updated: ${payload.title}`, description });
      }
    });

    connection.onreconnecting(() => {
      if (isActive) setStatus('reconnecting');
    });
    connection.onreconnected(() => {
      if (isActive) setStatus('connected');
    });
    connection.onclose(() => {
      if (isActive) setStatus('disconnected');
    });

    // withAutomaticReconnect only retries AFTER a first successful connect, so we
    // own the initial-connect retry loop here (backoff, capped) and stop once the
    // effect is torn down.
    const connectWithRetry = (attempt: number) => {
      if (!isActive) return;
      setStatus(attempt === 0 ? 'connecting' : 'reconnecting');

      startPromise = connection
        .start()
        .then(() => {
          startPromise = null;
          if (isActive) setStatus('connected');
        })
        .catch(() => {
          startPromise = null;
          if (!isActive) return;
          setStatus('disconnected');
          const delay = Math.min(INITIAL_RETRY_BASE_MS * 2 ** attempt, INITIAL_RETRY_MAX_MS);
          retryHandle = window.setTimeout(() => connectWithRetry(attempt + 1), delay);
        });
    };

    connectWithRetry(0);

    return () => {
      isActive = false;
      if (retryHandle !== null) window.clearTimeout(retryHandle);
      if (invalidateHandle !== null) window.clearTimeout(invalidateHandle);
      connection.off(VACANCY_CHANGED_EVENT);

      // Never stop mid-negotiation: wait for any pending start() to settle first,
      // then stop only if the connection actually reached a live/connecting state.
      const stop = () => {
        if (connection.state !== HubConnectionState.Disconnected) {
          void connection.stop();
        }
      };
      if (startPromise) {
        void startPromise.then(stop, stop);
      } else {
        stop();
      }
    };
  }, [queryClient, pushToast]);

  return status;
}
