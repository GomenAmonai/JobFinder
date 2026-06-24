import { useAuth } from '../hooks/use-auth';
import { ConnectionIndicator } from './ConnectionIndicator';
import { LogOutIcon, RadarIcon, UserIcon } from './icons';

import type { StreamStatus } from '../hooks/use-vacancy-stream';

export type AppView = 'browse' | 'applications' | 'employer';

interface HeaderProps {
  connectionStatus: StreamStatus;
  view: AppView;
  onViewChange: (view: AppView) => void;
  onSignIn: () => void;
}

export function Header({ connectionStatus, view, onViewChange, onSignIn }: HeaderProps) {
  const { user, isAuthenticated, isEmployer, logout } = useAuth();

  const tabs: { id: AppView; label: string; show: boolean }[] = [
    { id: 'browse', label: 'Browse', show: true },
    { id: 'applications', label: 'My applications', show: isAuthenticated },
    { id: 'employer', label: 'Employer', show: isEmployer },
  ];

  return (
    <header className="header">
      <div className="header__inner">
        <div className="header__brand">
          <span className="header__logo">
            <RadarIcon />
          </span>
          <div>
            <div className="header__title">
              JobRadar <span className="header__title-accent">— remote .NET / backend</span>
            </div>
            <div className="header__subtitle">live aggregator</div>
          </div>
        </div>

        <nav className="nav" aria-label="Sections">
          {tabs
            .filter((tab) => tab.show)
            .map((tab) => (
              <button
                key={tab.id}
                type="button"
                className="nav__tab"
                aria-current={view === tab.id ? 'page' : undefined}
                onClick={() => onViewChange(tab.id)}
              >
                {tab.label}
              </button>
            ))}
        </nav>

        <div className="header__right">
          <ConnectionIndicator status={connectionStatus} />
          {isAuthenticated ? (
            <div className="user-chip">
              <UserIcon />
              <span className="user-chip__email">{user?.email}</span>
              {isEmployer && <span className="user-chip__role">employer</span>}
              <button type="button" className="icon-button" aria-label="Sign out" onClick={logout}>
                <LogOutIcon />
              </button>
            </div>
          ) : (
            <button type="button" className="page-button" onClick={onSignIn}>
              Sign in
            </button>
          )}
        </div>
      </div>
    </header>
  );
}
