import { REPOSITORY_URL } from '../api/config';
import { resetDemoData } from '../api/demo/server';
import { clearTokens } from '../api/token-store';

// Shown only in demo mode: says plainly that the data is simulated and where the
// real backend lives, and lets a visitor start over from the seeded state.
export function DemoBanner() {
  const reset = () => {
    resetDemoData();
    clearTokens();
    window.location.reload();
  };

  return (
    <div className="demo-banner" role="note">
      <div className="demo-banner__inner">
        <span className="demo-banner__tag">Demo</span>
        <p className="demo-banner__text">
          The API is simulated in your browser. The real stack (.NET 10, Kafka, PostgreSQL, SignalR) runs
          locally with <code>docker compose up</code>.
        </p>
        <div className="demo-banner__actions">
          <a className="demo-banner__link" href={REPOSITORY_URL} target="_blank" rel="noopener noreferrer">
            Source on GitHub
          </a>
          <button type="button" className="demo-banner__reset" onClick={reset}>
            Reset demo
          </button>
        </div>
      </div>
    </div>
  );
}
