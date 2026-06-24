import { useState } from 'react';

import { ApiError } from '../api/http';
import { useAuth } from '../hooks/use-auth';
import { Modal } from './Modal';

import type { FormEvent } from 'react';
import type { UserRole } from '../types/auth';

type Mode = 'login' | 'register';

function describeError(error: unknown, mode: Mode): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'Wrong email or password.';
    if (error.status === 409) return 'That email is already registered.';
    if (error.status === 429) return 'Too many attempts — wait a minute and try again.';
    if (error.status === 400) return 'Check the form: valid email and an 8–128 character password.';
  }
  return mode === 'login' ? 'Sign-in failed. Is the API running?' : 'Sign-up failed. Is the API running?';
}

export function AuthDialog({ onClose }: { onClose: () => void }) {
  const { login, register } = useAuth();
  const [mode, setMode] = useState<Mode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<UserRole>('Candidate');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      if (mode === 'login') {
        await login({ email: email.trim(), password });
      } else {
        await register({ email: email.trim(), password, role });
      }
      onClose();
    } catch (err) {
      setError(describeError(err, mode));
    } finally {
      setPending(false);
    }
  };

  return (
    <Modal title={mode === 'login' ? 'Sign in' : 'Create account'} onClose={onClose}>
      <div className="segmented" role="tablist" aria-label="Auth mode">
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'login'}
          className="segmented__option"
          onClick={() => setMode('login')}
        >
          Sign in
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'register'}
          className="segmented__option"
          onClick={() => setMode('register')}
        >
          Register
        </button>
      </div>

      <form className="form" onSubmit={submit}>
        <label className="field">
          <span className="field__label">Email</span>
          <input
            type="email"
            className="text-input"
            autoComplete="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>

        <label className="field">
          <span className="field__label">Password</span>
          <input
            type="password"
            className="text-input"
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
            required
            minLength={8}
            maxLength={128}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>

        {mode === 'register' && (
          <div className="field" role="group" aria-label="Account type">
            <span className="field__label">I am a</span>
            <div className="chips">
              {(['Candidate', 'Employer'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  className="chip"
                  aria-pressed={role === option}
                  onClick={() => setRole(option)}
                >
                  {option}
                </button>
              ))}
            </div>
          </div>
        )}

        {error && <p className="form__error">{error}</p>}

        <button type="submit" className="button-primary" disabled={pending}>
          {pending ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}
        </button>
      </form>
    </Modal>
  );
}
