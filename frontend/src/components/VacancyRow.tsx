import { formatShortDate, splitSkills } from '../api/format';
import { LevelBadge, MarketBadge, StackBadge } from './Badges';
import { ExternalLinkIcon } from './icons';

import type { VacancyDto } from '../types/vacancy';

interface VacancyRowProps {
  vacancy: VacancyDto;
  applied?: boolean;
  onApply?: (vacancy: VacancyDto) => void;
}

export function VacancyRow({ vacancy, applied, onApply }: VacancyRowProps) {
  const skills = splitSkills(vacancy.skills).join(', ');
  const subline = [vacancy.company, vacancy.location].filter(Boolean).join(' · ');

  return (
    <tr>
      <td className="cell-date" data-label="Date">
        {formatShortDate(vacancy.publishedAt)}
      </td>
      <td className="cell-position">
        {vacancy.url ? (
          <a className="cell-title" href={vacancy.url} target="_blank" rel="noopener noreferrer">
            {vacancy.title}
            <ExternalLinkIcon className="cell-title__ext" />
          </a>
        ) : (
          <span className="cell-title">{vacancy.title}</span>
        )}
        {subline && <span className="cell-sub">{subline}</span>}
      </td>
      <td data-label="Level">
        <LevelBadge level={vacancy.level} />
      </td>
      <td data-label="Market">
        <MarketBadge market={vacancy.market} />
      </td>
      <td data-label="Stack">
        <StackBadge stack={vacancy.stack} />
      </td>
      <td className="cell-salary" data-label="Salary">
        {vacancy.salaryRaw ?? <span className="cell-muted">not stated</span>}
      </td>
      <td className="cell-skills" data-label="Skills" title={skills || undefined}>
        {skills ? <span className="cell-skills__text">{skills}</span> : <span className="cell-muted">—</span>}
      </td>
      <td className="cell-source" data-label="Source">
        {vacancy.source}
      </td>
      {onApply && (
        <td className="cell-action">
          {applied ? (
            <span className="applied-tag">Applied</span>
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
