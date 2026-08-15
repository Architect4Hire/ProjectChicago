import { type FC } from 'react';
import { Surface } from '@/design-system';
import type { ClientDuplicateWarning } from '@/api/clients';
import { DUPLICATE_MATCH_LABELS } from '../types';

interface ClientDuplicateWarningsProps {
  duplicates: ClientDuplicateWarning[];
}

/**
 * CLIENT-004: non-blocking warning surfaced after a Client is created when likely duplicates
 * were found. Uses explicit "Possible duplicate" text (not color alone, ACCESS-005) and an
 * assertive live region so it is announced immediately (accessibility.md).
 */
export const ClientDuplicateWarnings: FC<ClientDuplicateWarningsProps> = ({ duplicates }) => {
  if (duplicates.length === 0) {
    return null;
  }

  return (
    <Surface
      role="alert"
      radius="lg"
      className="border-warning-300 bg-warning-50 p-4 dark:border-warning-700 dark:bg-warning-900/20"
    >
      <p className="text-sm font-semibold text-warning-800 dark:text-warning-300">
        Possible duplicate {duplicates.length === 1 ? 'client' : 'clients'} found
      </p>
      <p className="mt-1 text-sm text-warning-700 dark:text-warning-400">
        The client was still created. Review the matches below before continuing.
      </p>
      <ul className="mt-3 flex flex-col gap-2">
        {duplicates.map((duplicate) => (
          <li
            key={duplicate.clientId}
            className="rounded-md border border-warning-200 bg-white px-3 py-2 text-sm dark:border-warning-800 dark:bg-gray-900"
          >
            <span className="font-medium text-gray-900 dark:text-white">{duplicate.name}</span>
            <span className="ml-2 text-gray-500 dark:text-gray-400">
              matched on {duplicate.matchedOn.map((field) => DUPLICATE_MATCH_LABELS[field]).join(', ')}
            </span>
          </li>
        ))}
      </ul>
    </Surface>
  );
};
