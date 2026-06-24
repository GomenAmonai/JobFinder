import { request } from './http';

import type { ApplicationDto, ApplicationStatus } from '../types/applications';

export function applyToVacancy(vacancyId: number, coverLetter?: string): Promise<ApplicationDto> {
  return request<ApplicationDto>(`/vacancies/${vacancyId}/applications`, {
    method: 'POST',
    body: { coverLetter: coverLetter || null },
    auth: true,
  });
}

export function listApplications(signal?: AbortSignal): Promise<ApplicationDto[]> {
  return request<ApplicationDto[]>('/me/applications', { auth: true, signal });
}

export function changeApplicationStatus(
  id: number,
  status: ApplicationStatus,
  version: string,
): Promise<ApplicationDto> {
  return request<ApplicationDto>(`/me/applications/${id}/status`, {
    method: 'PATCH',
    body: { status, version },
    auth: true,
  });
}

export function deleteApplication(id: number): Promise<void> {
  return request<void>(`/me/applications/${id}`, { method: 'DELETE', auth: true });
}
