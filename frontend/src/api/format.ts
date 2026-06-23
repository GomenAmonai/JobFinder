const DATE_FORMATTER = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
});

export function formatPublishedDate(value: string | null): string {
  if (!value) return '—';
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return '—';
  return DATE_FORMATTER.format(parsed);
}

export function splitSkills(skills: string | null): string[] {
  if (!skills) return [];
  return skills
    .split(/[,;|]/)
    .map((skill) => skill.trim())
    .filter((skill) => skill.length > 0);
}
