import { TableSkeleton } from './StatePanels';
import { VacancyRow } from './VacancyRow';

import type { VacancyDto } from '../types/vacancy';

const COLUMNS = ['Date', 'Position', 'Level', 'Market', 'Stack', 'Salary', 'Skills', 'Source'] as const;

interface VacancyTableProps {
  vacancies: VacancyDto[];
  isLoading: boolean;
  appliedVacancyIds?: Set<number>;
  onApply?: (vacancy: VacancyDto) => void;
}

export function VacancyTable({ vacancies, isLoading, appliedVacancyIds, onApply }: VacancyTableProps) {
  const columnCount = COLUMNS.length + (onApply ? 1 : 0);

  return (
    <div className="table-wrap">
      <table className="table">
        <caption className="visually-hidden">Remote .NET and backend vacancies</caption>
        <thead>
          <tr>
            {COLUMNS.map((column) => (
              <th key={column} scope="col" className={`col-${column.toLowerCase()}`}>
                {column}
              </th>
            ))}
            {onApply && (
              <th scope="col">
                <span className="visually-hidden">Apply</span>
              </th>
            )}
          </tr>
        </thead>
        {isLoading ? (
          <TableSkeleton columns={columnCount} />
        ) : (
          <tbody>
            {vacancies.map((vacancy) => (
              <VacancyRow
                key={vacancy.id}
                vacancy={vacancy}
                applied={appliedVacancyIds?.has(vacancy.id)}
                onApply={onApply}
              />
            ))}
          </tbody>
        )}
      </table>
    </div>
  );
}
