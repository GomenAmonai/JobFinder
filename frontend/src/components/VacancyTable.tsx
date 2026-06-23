import { TableSkeleton } from './StatePanels';
import { VacancyRow } from './VacancyRow';

import type { VacancyDto } from '../types/vacancy';

const COLUMNS = [
  'Source',
  'Market',
  'Level',
  'Stack',
  'Title',
  'Company',
  'Location',
  'Salary',
  'Skills',
  'Published',
] as const;

interface VacancyTableProps {
  vacancies: VacancyDto[];
  isLoading: boolean;
}

export function VacancyTable({ vacancies, isLoading }: VacancyTableProps) {
  return (
    <div className="table-wrap">
      <table className="table">
        <caption className="visually-hidden">Remote .NET and backend vacancies</caption>
        <thead>
          <tr>
            {COLUMNS.map((column) => (
              <th key={column} scope="col">
                {column}
              </th>
            ))}
          </tr>
        </thead>
        {isLoading ? (
          <TableSkeleton />
        ) : (
          <tbody>
            {vacancies.map((vacancy) => (
              <VacancyRow key={vacancy.id} vacancy={vacancy} />
            ))}
          </tbody>
        )}
      </table>
    </div>
  );
}
