import { request } from './http';

import type { SaveFilterInput, SavedFilterDto } from '../types/filters';

export function listFilters(signal?: AbortSignal): Promise<SavedFilterDto[]> {
  return request<SavedFilterDto[]>('/me/filters', { auth: true, signal });
}

export function createFilter(input: SaveFilterInput): Promise<SavedFilterDto> {
  return request<SavedFilterDto>('/me/filters', { method: 'POST', body: input, auth: true });
}

export function updateFilter(
  id: number,
  input: SaveFilterInput & { version: string },
): Promise<SavedFilterDto> {
  return request<SavedFilterDto>(`/me/filters/${id}`, { method: 'PUT', body: input, auth: true });
}

export function deleteFilter(id: number): Promise<void> {
  return request<void>(`/me/filters/${id}`, { method: 'DELETE', auth: true });
}
