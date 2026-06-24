import { formatPublishedDate, splitSkills } from '../api/format';
import { LevelBadge, MarketBadge, StackBadge } from './Badges';
import { CheckIcon, ExternalLinkIcon } from './icons';

import type { VacancyDto } from '../types/vacancy';

const MAX_VISIBLE_SKILLS = 6;

interface VacancyRowProps {
  vacancy: VacancyDto;
  applied?: boolean;
  onApply?: (vacancy: VacancyDto) => void;
}

export function VacancyRow({ vacancy, applied, onApply }: VacancyRowProps) {
  const skills = splitSkills(vacancy.skills);
  const visibleSkills = skills.slice(0, MAX_VISIBLE_SKILLS);
  const overflowCount = skills.length - visibleSkills.length;

  return (
    <tr>
      <td>
        <span className="cell-source">{vacancy.source}</span>
      </td>
      <td>
        <MarketBadge market={vacancy.market} />
      </td>
      <td>
        <LevelBadge level={vacancy.level} />
      </td>
      <td>
        <StackBadge stack={vacancy.stack} />
      </td>
      <td>
        {vacancy.url ? (
          <a
            className="cell-title"
            href={vacancy.url}
            target="_blank"
            rel="noopener noreferrer"
            title={vacancy.title}
          >
            {vacancy.title}
            <ExternalLinkIcon className="cell-title__ext" />
          </a>
        ) : (
          <span className="cell-title cell-title--plain" title={vacancy.title}>
            {vacancy.title}
          </span>
        )}
      </td>
      <td>
        <span className="cell-company">{vacancy.company ?? '—'}</span>
      </td>
      <td>
        <span className="cell-muted">{vacancy.location ?? '—'}</span>
      </td>
      <td>
        <span className="cell-muted">{vacancy.salaryRaw ?? '—'}</span>
      </td>
      <td>
        {visibleSkills.length > 0 ? (
          <div className="skills">
            {visibleSkills.map((skill) => (
              <span key={skill} className="skill-tag">
                {skill}
              </span>
            ))}
            {overflowCount > 0 && <span className="skill-tag">+{overflowCount}</span>}
          </div>
        ) : (
          <span className="cell-muted">—</span>
        )}
      </td>
      <td>
        <span className="cell-mono">{formatPublishedDate(vacancy.publishedAt)}</span>
      </td>
      {onApply && (
        <td>
          {applied ? (
            <span className="applied-tag">
              <CheckIcon size={13} /> Applied
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
