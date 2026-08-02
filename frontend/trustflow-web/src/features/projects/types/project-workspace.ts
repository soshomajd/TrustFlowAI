import type { ProjectStatus } from "@/features/projects/types/project";

export type ProjectWorkspace = {
  id: string;

  clientId: string | null;
  clientFullName: string;

  freelancerId: string | null;
  freelancerFullName: string | null;

  title: string;
  description: string;

  budget: number;
  deadline: string;

  status: ProjectStatus;
  createdAt: string;
};
