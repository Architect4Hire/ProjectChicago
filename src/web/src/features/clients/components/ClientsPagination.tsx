import { type FC } from 'react';

interface ClientsPaginationProps {
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}

export const ClientsPagination: FC<ClientsPaginationProps> = ({
  pageNumber,
  pageSize,
  totalPages,
  totalCount,
  onPageChange,
  onPageSizeChange,
}) => {
  const startItem = (pageNumber - 1) * pageSize + 1;
  const endItem = Math.min(pageNumber * pageSize, totalCount);

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-950 sm:flex-row sm:items-center sm:justify-between">
      <div className="text-sm text-gray-600 dark:text-gray-400">
        Showing <span className="font-medium">{startItem}</span> to <span className="font-medium">{endItem}</span> of{' '}
        <span className="font-medium">{totalCount}</span> results
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="flex items-center gap-2">
          <label htmlFor="page-size" className="text-sm text-gray-600 dark:text-gray-400">
            Items per page:
          </label>
          <select
            id="page-size"
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            className="rounded-md border border-gray-300 px-2 py-1 text-sm dark:border-gray-600 dark:bg-gray-900 dark:text-white"
            aria-label="Items per page"
          >
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
          </select>
        </div>

        <div className="flex gap-2">
          <button
            onClick={() => onPageChange(pageNumber - 1)}
            disabled={pageNumber <= 1}
            className="rounded-md border border-gray-300 px-3 py-1 text-sm text-gray-600 disabled:cursor-not-allowed disabled:opacity-50 dark:border-gray-600 dark:text-gray-400 hover:enabled:bg-gray-50 dark:hover:enabled:bg-gray-900"
            aria-label="Previous page"
          >
            ← Previous
          </button>

          <div className="flex items-center gap-1">
            {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
              let pageNum: number;
              if (totalPages <= 5) {
                pageNum = i + 1;
              } else if (pageNumber <= 3) {
                pageNum = i + 1;
              } else if (pageNumber >= totalPages - 2) {
                pageNum = totalPages - 4 + i;
              } else {
                pageNum = pageNumber - 2 + i;
              }
              return (
                <button
                  key={pageNum}
                  onClick={() => onPageChange(pageNum)}
                  className={`min-w-8 rounded-md px-2 py-1 text-sm ${
                    pageNumber === pageNum
                      ? 'bg-brand-600 text-white dark:bg-brand-500'
                      : 'border border-gray-300 text-gray-600 dark:border-gray-600 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-900'
                  }`}
                  aria-current={pageNumber === pageNum ? 'page' : undefined}
                  aria-label={`Go to page ${pageNum}`}
                >
                  {pageNum}
                </button>
              );
            })}
          </div>

          <button
            onClick={() => onPageChange(pageNumber + 1)}
            disabled={pageNumber >= totalPages}
            className="rounded-md border border-gray-300 px-3 py-1 text-sm text-gray-600 disabled:cursor-not-allowed disabled:opacity-50 dark:border-gray-600 dark:text-gray-400 hover:enabled:bg-gray-50 dark:hover:enabled:bg-gray-900"
            aria-label="Next page"
          >
            Next →
          </button>
        </div>
      </div>
    </div>
  );
};
