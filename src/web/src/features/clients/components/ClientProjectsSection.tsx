import { type FC } from 'react';
import { useNavigate } from 'react-router-dom';
import { Badge, Button, Card, Cluster, Stack } from '@/design-system';
import type { ClientDetailProjectSummary } from '@/api/clients';
import { PRIORITY_LABELS, PRIORITY_TONES, PROJECT_STATUS_LABELS, PROJECT_STATUS_TONES } from '../types';

interface ClientProjectsSectionProps {
  clientId: string;
  activeProjects: ClientDetailProjectSummary[];
  historicalProjects: ClientDetailProjectSummary[];
}

/**
 * CLIENT-030/031: active and historical Project summaries for a Client, plus navigation to the
 * Client's Projects. Project-detail navigation is deferred - no per-Project detail route exists
 * yet (PROJECT-030 is a separate, not-yet-built page), so rows are summary-only for now.
 */
export const ClientProjectsSection: FC<ClientProjectsSectionProps> = ({
  clientId,
  activeProjects,
  historicalProjects,
}) => {
  const navigate = useNavigate();

  return (
    <Card>
      <Stack className="gap-5">
        <Cluster className="justify-between">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">Projects</h2>
          <Button variant="outline" size="sm" onClick={() => navigate(`/projects?clientId=${clientId}`)}>
            View all Projects
          </Button>
        </Cluster>

        <ProjectList
          title="Active Projects"
          projects={activeProjects}
          emptyMessage="No active projects for this client."
        />
        <ProjectList
          title="Historical Projects"
          projects={historicalProjects}
          emptyMessage="No historical projects for this client."
        />
      </Stack>
    </Card>
  );
};

interface ProjectListProps {
  title: string;
  projects: ClientDetailProjectSummary[];
  emptyMessage: string;
}

const ProjectList: FC<ProjectListProps> = ({ title, projects, emptyMessage }) => (
  <div>
    <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300">
      {title} <span className="text-gray-400 dark:text-gray-600">({projects.length})</span>
    </h3>

    {projects.length === 0 ? (
      <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">{emptyMessage}</p>
    ) : (
      <ul className="mt-2 flex flex-col gap-2">
        {projects.map((project) => (
          <li
            key={project.id}
            className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-gray-200 px-3 py-2 dark:border-gray-800"
          >
            <span className="text-sm font-medium text-gray-900 dark:text-white">{project.name}</span>
            <Cluster className="gap-2">
              <Badge tone={PRIORITY_TONES[project.priority]}>{PRIORITY_LABELS[project.priority]}</Badge>
              <Badge tone={PROJECT_STATUS_TONES[project.status]}>{PROJECT_STATUS_LABELS[project.status]}</Badge>
            </Cluster>
          </li>
        ))}
      </ul>
    )}
  </div>
);
