export type UserRole = 'Candidate' | 'Employer';

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

export interface AuthUser {
  id: string;
  email: string;
  role: UserRole;
}

export interface RegisterInput {
  email: string;
  password: string;
  displayName?: string;
  role: UserRole;
}

export interface LoginInput {
  email: string;
  password: string;
}
