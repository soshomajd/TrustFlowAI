import type { GetProjectProposalsParams } from "@/features/proposals/types/proposal";

export const proposalQueryKeys = {
  all: ["proposals"] as const,

  projectLists: () => [...proposalQueryKeys.all, "project-list"] as const,

  projectList: (
    projectId: string,
    params: Required<GetProjectProposalsParams>,
  ) => [...proposalQueryKeys.projectLists(), projectId, params] as const,

  accept: (projectId: string, proposalId: string) =>
    [...proposalQueryKeys.all, "accept", projectId, proposalId] as const,

  reject: (projectId: string, proposalId: string) =>
    [...proposalQueryKeys.all, "reject", projectId, proposalId] as const,

  create: (projectId: string) =>
    [...proposalQueryKeys.all, "create", projectId] as const,
};
