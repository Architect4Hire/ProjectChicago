import { type FC } from 'react';
import { Cluster } from '@/design-system';

interface ProjectsPaginationProps {
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}

export const ProjectsPagination: FC<ProjectsPaginationProps> = ({
  pageNumber,
  pageSize,
  totalPages,
  totalCount,
  onPageChange,
  onPageSizeChange,
}) => {
  const startRecord = (pageNumber - 1) * pageSize + 1;
  const endRecord = Math.min(pageNumber * pageSize, totalCount);

  return (
    <div className="flex flex-col items-center justify-between gap-4 rounded-lg border border-gray-200 bg-white px-6 py-4 sm:flex-row dark:border-gray-800 dark:bg-gray-950">
      <div className="text-sm text-gray-600 dark:text-gray-400">
        Showing {startRecord} to {endRecord} of {totalCount} projects
      </div>

      <Cluster>
        <div className="flex items-center gap-2">
          <label htmlFor="page-size" className="text-sm text-gray-600 dark:text-gray-400">
            Per page:
          </label>
          <select
            id="page-size"
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            className="rounded border border-gray-300 px-2 py-1 text-sm dark:border-gray-700 dark:bg-gray-900 dark:text-white"
            aria-label="Projects per page"
          >
            <option value={10}>10</option>
            <option value={25}>25</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
          </select>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => onPageChange(Math.max(1, pageNumber - 1))}
            disabled={pageNumber === 1}
            className="rounded border border-gray-300 px-3 py-1 text-sm font-medium text-gray-700 disabled:cursor-not-allowed disabled:opacity-50 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-900"
            aria-label="Previous page"
          >
            Previous
          </button>

          <span className="text-sm text-gray-600 dark:text-gray-400">
            Page {pageNumber} of {totalPages}
          </span>

          <button
            onClick={() => onPageChange(Math.min(totalPages, pageNumber + 1))}
            disabled={pageNumber === totalPages}
            className="rounded border border-gray-300 px-3 py-1 text-sm font-medium text-gray-700 disabled:cursor-not-allowed disabled:opacity-50 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-900"
            aria-label="Next page"
          >
            Next
          </button>
        </div>
      </Cluster>
    </div>
  );
};
