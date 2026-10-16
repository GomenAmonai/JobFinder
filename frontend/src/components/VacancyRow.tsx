import { formatShortDate, splitSkills } from '../api/format';
import { LevelBadge, MarketBadge, StackBadge } from './Badges';
import { CheckIcon, ExternalLinkIcon } from './icons';

import type { VacancyDto } from '../types/vacancy';

const MAX_VISIBLE_SKILLS = 3;

interface VacancyRowProps {
  vacancy: VacancyDto;
  applied?: boolean;
  onApply?: (vacancy: VacancyDto) => void;
}

export function VacancyRow({ vacancy, applied, onApply }: VacancyRowProps) {
  const skills = splitSkills(vacancy.skills);
  const visibleSkills = skills.slice(0, MAX_VISIBLE_SKILLS);
  const hiddenCount = skills.length - visibleSkills.length;
  const subline = [vacancy.company, vacancy.location].filter(Boolean).join(' · ');

  return (
    <tr>
      <td className="cell-position">
        {vacancy.url ? (
          <a className="cell-title" href={vacancy.url} target="_blank" rel="noopener noreferrer">
            {vacancy.title}
            <ExternalLinkIcon className="cell-title__ext" />
          </a>
        ) : (
          <span className="cell-title">{vacancy.title}</span>
        )}
        <div className="cell-sub">
          <StackBadge stack={vacancy.stack} />
          {subline && <span>{subline}</span>}
        </div>
      </td>
      <td>
        <LevelBadge level={vacancy.level} />
      </td>
      <td className="cell-market">
        <MarketBadge market={vacancy.market} />
      </td>
      <td className="cell-salary">
        {vacancy.salaryRaw ?? <span className="cell-muted">Not stated</span>}
      </td>
      <td className="cell-skills" title={skills.join(', ') || undefined}>
        {visibleSkills.length > 0 ? (
          <div className="skills">
            {visibleSkills.map((skill) => (
              <span key={skill} className="skill-tag">
                {skill}
              </span>
            ))}
            {hiddenCount > 0 && <span className="skill-tag skill-tag--more">+{hiddenCount}</span>}
          </div>
        ) : (
          <span className="cell-muted">—</span>
        )}
      </td>
      <td className="cell-source">{vacancy.source}</td>
      <td className="cell-date">{formatShortDate(vacancy.publishedAt)}</td>
      {onApply && (
        <td className="cell-action">
          {applied ? (
            <span className="applied-tag">
              <CheckIcon size={12} /> Applied
            </span>
          ) : (
            <button type="button" className="apply-button" onClick={() => onApply(vacancy)}>
              Apply
            </button>
          )}
        </td>
      )}
    </tr>
  );
}
