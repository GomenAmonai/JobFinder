import { request } from './http';

import type { ApplicationDto, ApplicationStatus, EmployerApplicationDto } from '../types/applications';
import type { VacancyDto } from '../types/vacancy';

export interface PostVacancyInput {
  title: string;
  company?: string;
  location?: string;
  salaryRaw?: string;
  skills?: string;
  url?: string;
}

export function postVacancy(input: PostVacancyInput): Promise<VacancyDto> {
  return request<VacancyDto>('/employer/vacancies', { method: 'POST', body: input, auth: true });
}

export function listEmployerApplications(signal?: AbortSignal): Promise<EmployerApplicationDto[]> {
  return request<EmployerApplicationDto[]>('/employer/applications', { auth: true, signal });
}

export function changeEmployerApplicationStatus(
  id: number,
  status: ApplicationStatus,
  version: string,
): Promise<ApplicationDto> {
  return request<ApplicationDto>(`/employer/applications/${id}/status`, {
    method: 'PATCH',
    body: { status, version },
    auth: true,
  });
}
