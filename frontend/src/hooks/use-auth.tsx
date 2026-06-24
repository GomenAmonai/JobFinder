import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';

import { fetchMe, loginUser, registerUser } from '../api/auth';
import { clearTokens, getTokens, setTokens, subscribeTokens } from '../api/token-store';

import type { ReactNode } from 'react';
import type { AuthUser, LoginInput, RegisterInput } from '../types/auth';

interface AuthContextValue {
  user: AuthUser | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  isEmployer: boolean;
  login: (input: LoginInput) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(() => getTokens() !== null);

  // Resolve the current user from a persisted token on mount, and drop the user
  // whenever the token store empties — logout, or a failed refresh in the HTTP layer.
  useEffect(() => {
    let active = true;

    const resolve = async () => {
      if (!getTokens()) {
        if (active) {
          setUser(null);
          setIsLoading(false);
        }
        return;
      }
      try {
        const me = await fetchMe();
        if (active) setUser(me);
      } catch {
        if (active) setUser(null);
      } finally {
        if (active) setIsLoading(false);
      }
    };

    void resolve();
    const unsubscribe = subscribeTokens(() => {
      if (!getTokens() && active) setUser(null);
    });
    return () => {
      active = false;
      unsubscribe();
    };
  }, []);

  const login = useCallback(async (input: LoginInput) => {
    setTokens(await loginUser(input));
    setUser(await fetchMe());
  }, []);

  const register = useCallback(async (input: RegisterInput) => {
    setTokens(await registerUser(input));
    setUser(await fetchMe());
  }, []);

  const logout = useCallback(() => {
    clearTokens();
    setUser(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      isAuthenticated: user !== null,
      isEmployer: user?.role === 'Employer',
      login,
      register,
      logout,
    }),
    [user, isLoading, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
