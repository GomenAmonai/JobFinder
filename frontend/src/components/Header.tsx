import { ConnectionIndicator } from './ConnectionIndicator';
import { RadarIcon } from './icons';

import type { StreamStatus } from '../hooks/use-vacancy-stream';

export function Header({ connectionStatus }: { connectionStatus: StreamStatus }) {
  return (
    <header className="header">
      <div className="header__inner">
        <div className="header__brand">
          <span className="header__logo">
            <RadarIcon />
          </span>
          <div>
            <div className="header__title">
              JobRadar <span className="header__title-accent">— remote .NET / backend vacancies</span>
            </div>
            <div className="header__subtitle">live aggregator</div>
          </div>
        </div>
        <ConnectionIndicator status={connectionStatus} />
      </div>
    </header>
  );
}
