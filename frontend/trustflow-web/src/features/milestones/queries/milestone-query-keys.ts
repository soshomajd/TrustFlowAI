export const milestoneQueryKeys = {
  all: ["milestones"] as const,

  projectLists: () => [...milestoneQueryKeys.all, "project-list"] as const,

  projectList: (projectId: string) =>
    [...milestoneQueryKeys.projectLists(), projectId] as const,

  detail: (projectId: string, milestoneId: string) =>
    [...milestoneQueryKeys.all, "detail", projectId, milestoneId] as const,
  create: (projectId: string) =>
    [...milestoneQueryKeys.all, "create", projectId] as const,
};
