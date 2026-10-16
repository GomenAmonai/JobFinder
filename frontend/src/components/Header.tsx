import { useAuth } from '../hooks/use-auth';
import { ConnectionIndicator } from './ConnectionIndicator';
import { LogOutIcon, RadarIcon } from './icons';

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
    { id: 'browse', label: 'Vacancies', show: true },
    { id: 'applications', label: 'Applications', show: isAuthenticated },
    { id: 'employer', label: 'Employer', show: isEmployer },
  ];
  const visibleTabs = tabs.filter((tab) => tab.show);

  return (
    <header className="header">
      <div className="header__inner">
        <div className="header__brand">
          <span className="header__logo" aria-hidden="true">
            <RadarIcon size={16} />
          </span>
          <span className="header__title">JobRadar</span>
        </div>

        {visibleTabs.length > 1 && (
          <nav className="nav" aria-label="Sections">
            {visibleTabs.map((tab) => (
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
        )}

        <div className="header__right">
          <ConnectionIndicator status={connectionStatus} />
          {isAuthenticated ? (
            <div className="user-chip">
              <span className="user-chip__avatar" aria-hidden="true">
                {user?.email.charAt(0).toUpperCase()}
              </span>
              <span className="user-chip__email">{user?.email}</span>
              {isEmployer && <span className="user-chip__role">Employer</span>}
              <button type="button" className="icon-button" aria-label="Sign out" onClick={logout}>
                <LogOutIcon />
              </button>
            </div>
          ) : (
            <button type="button" className="button-primary button-primary--sm" onClick={onSignIn}>
              Sign in
            </button>
          )}
        </div>
      </div>
    </header>
  );
}
