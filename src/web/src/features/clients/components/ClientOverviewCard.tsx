import { type FC, type ReactNode } from 'react';
import { Badge, Card, Stack } from '@/design-system';
import type { ClientDetailRecord } from '@/api/clients';
import { LIFECYCLE_STATUSES, LIFECYCLE_STATUS_TONES } from '../types';

interface ClientOverviewCardProps {
  client: ClientDetailRecord;
  lifecycleControl?: ReactNode;
  archiveControl?: ReactNode;
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

/**
 * CLIENT-030: Client information, lifecycle status, and assigned owner in one consolidated card.
 */
export const ClientOverviewCard: FC<ClientOverviewCardProps> = ({ client, lifecycleControl, archiveControl }) => {
  const address = [client.addressLine, client.city, client.stateOrProvince, client.postalCode, client.country]
    .filter(Boolean)
    .join(', ');

  return (
    <Card>
      <Stack className="gap-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-3">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">{client.name}</h2>
            <Badge tone={LIFECYCLE_STATUS_TONES[client.lifecycleStatus]}>
              {LIFECYCLE_STATUSES[client.lifecycleStatus]}
            </Badge>
          </div>
          {lifecycleControl}
        </div>

        {archiveControl && (
          <div className="border-t border-gray-100 pt-4 dark:border-gray-800">{archiveControl}</div>
        )}

        {client.description && <p className="text-sm text-gray-600 dark:text-gray-400">{client.description}</p>}

        <dl className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          <DetailField label="Assigned owner" value={client.ownerUserId} />
          <DetailField label="Primary contact" value={client.primaryContactName} />
          <DetailField
            label="Email"
            value={client.primaryEmail}
            href={client.primaryEmail ? `mailto:${client.primaryEmail}` : undefined}
          />
          <DetailField
            label="Phone"
            value={client.primaryPhone}
            href={client.primaryPhone ? `tel:${client.primaryPhone}` : undefined}
          />
          <DetailField label="Website" value={client.website} href={client.website || undefined} external />
          <DetailField label="Address" value={address || undefined} />
          <DetailField label="Created" value={`${formatDate(client.createdAtUtc)} by ${client.createdBy}`} />
          <DetailField
            label="Last modified"
            value={`${formatDate(client.lastModifiedAtUtc)} by ${client.lastModifiedBy}`}
          />
        </dl>
      </Stack>
    </Card>
  );
};

interface DetailFieldProps {
  label: string;
  value?: string | null;
  href?: string;
  external?: boolean;
}

const DetailField: FC<DetailFieldProps> = ({ label, value, href, external }) => (
  <div>
    <dt className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-gray-400">{label}</dt>
    <dd className="mt-1 text-sm text-gray-900 dark:text-white">
      {value ? renderValue(value, href, external) : (
        <span className="text-gray-400 dark:text-gray-600">Not provided</span>
      )}
    </dd>
  </div>
);

function renderValue(value: string, href?: string, external?: boolean): ReactNode {
  if (!href) {
    return value;
  }

  return (
    <a
      href={href}
      target={external ? '_blank' : undefined}
      rel={external ? 'noreferrer' : undefined}
      className="text-brand-600 hover:underline dark:text-brand-400"
    >
      {value}
    </a>
  );
}
