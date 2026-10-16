// The UI copy is English, so dates use a fixed English locale instead of the
// visitor's system one (which mixed e.g. Russian month names into English text).
const DATE_FORMATTER = new Intl.DateTimeFormat('en-GB', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
});

const SHORT_DATE_FORMATTER = new Intl.DateTimeFormat('en-GB', {
  month: 'short',
  day: '2-digit',
});

function parseDate(value: string | null): Date | null {
  if (!value) return null;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export function formatPublishedDate(value: string | null): string {
  const parsed = parseDate(value);
  return parsed ? DATE_FORMATTER.format(parsed) : '—';
}

// Compact form for the vacancy table: "17 Oct".
export function formatShortDate(value: string | null): string {
  const parsed = parseDate(value);
  return parsed ? SHORT_DATE_FORMATTER.format(parsed) : '—';
}

export function splitSkills(skills: string | null): string[] {
  if (!skills) return [];
  return skills
    .split(/[,;|]/)
    .map((skill) => skill.trim())
    .filter((skill) => skill.length > 0);
}
