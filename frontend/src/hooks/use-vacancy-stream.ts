import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { VACANCIES_HUB_URL } from '../api/config';
import { getTokens } from '../api/token-store';
import { APPLICATIONS_QUERY_KEY } from './use-applications';
import { useAuth } from './use-auth';
import { useToasts } from './use-toasts';
import { VACANCIES_QUERY_KEY } from './use-vacancies';

import { STATUS_LABELS } from '../types/applications';
import type { ApplicationDto } from '../types/applications';
import type { VacancyChangedPayload } from '../types/vacancy';

export type StreamStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

const VACANCY_CHANGED_EVENT = 'VacancyChanged';
const MATCHED_VACANCY_EVENT = 'MatchedVacancy';
const APPLICATION_STATUS_EVENT = 'ApplicationStatusChanged';
const INVALIDATE_DEBOUNCE_MS = 500;
const INITIAL_RETRY_BASE_MS = 1000;
const INITIAL_RETRY_MAX_MS = 30_000;

export function useVacancyStream(): StreamStatus {
  const queryClient = useQueryClient();
  const { pushToast } = useToasts();
  const { user } = useAuth();
  const [status, setStatus] = useState<StreamStatus>('connecting');

  // Reconnect when identity changes (userId) so the JWT is attached and the user
  // starts/stops receiving their targeted MatchedVacancy / ApplicationStatusChanged events.
  const userId = user?.id ?? null;

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      // SignalR sends the token as ?access_token=; the API only honors it on /hubs.
      .withUrl(VACANCIES_HUB_URL, { accessTokenFactory: () => getTokens()?.accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    let isActive = true;
    let startPromise: Promise<void> | null = null;
    let retryHandle: number | null = null;
    let invalidateHandle: number | null = null;

    // Bursts from the ingestion worker can fire many events in quick succession;
    // collapse them into one refetch of the active vacancies query.
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

    // Targeted: a freshly-inserted vacancy matched one of this user's saved filters.
    connection.on(MATCHED_VACANCY_EVENT, (payload: VacancyChangedPayload) => {
      scheduleInvalidate();
      pushToast({
        variant: 'inserted',
        title: `Matches your filter: ${payload.title}`,
        description: [payload.company, payload.market].filter(Boolean).join(' · '),
      });
    });

    // Targeted: an employer moved this user's application to a new stage.
    connection.on(APPLICATION_STATUS_EVENT, (payload: ApplicationDto) => {
      void queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });
      pushToast({
        variant: 'updated',
        title: `Application: ${STATUS_LABELS[payload.status]}`,
        description: payload.vacancy.title,
      });
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
      connection.off(MATCHED_VACANCY_EVENT);
      connection.off(APPLICATION_STATUS_EVENT);

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
  }, [queryClient, pushToast, userId]);

  return status;
}
