import { request } from './http';

import type { AuthUser, LoginInput, RegisterInput, TokenPair } from '../types/auth';

export function registerUser(input: RegisterInput): Promise<TokenPair> {
  return request<TokenPair>('/auth/register', { method: 'POST', body: input });
}

export function loginUser(input: LoginInput): Promise<TokenPair> {
  return request<TokenPair>('/auth/login', { method: 'POST', body: input });
}

export function fetchMe(): Promise<AuthUser> {
  return request<AuthUser>('/auth/me', { auth: true });
}
