type LevelBucket = 'junior' | 'middle' | 'senior' | 'unknown';

function classifyLevel(level: string | null): LevelBucket {
  if (!level) return 'unknown';
  const normalized = level.toLowerCase();
  if (normalized.includes('junior') || normalized.includes('intern')) return 'junior';
  if (normalized.includes('senior') || normalized.includes('lead') || normalized.includes('+')) {
    return 'senior';
  }
  if (normalized.includes('middle') || normalized.includes('mid')) {
    // "mid/unknown" should read as gray per the brief; a bare "middle" is blue.
    return normalized.includes('unknown') ? 'unknown' : 'middle';
  }
  return 'unknown';
}

export function MarketBadge({ market }: { market: string | null }) {
  if (!market) return <span className="cell-muted">—</span>;
  return <span className="badge badge--market">{market}</span>;
}

export function LevelBadge({ level }: { level: string | null }) {
  const bucket = classifyLevel(level);
  const label = level ?? 'unknown';
  return <span className={`badge badge--level-${bucket}`}>{label}</span>;
}

export function StackBadge({ stack }: { stack: string | null }) {
  if (!stack) return <span className="cell-muted">—</span>;
  return <span className="badge badge--stack">{stack}</span>;
}
