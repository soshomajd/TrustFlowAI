export const milestoneQueryKeys = {
  all: ["milestones"] as const,

  projectLists: () => [...milestoneQueryKeys.all, "project-list"] as const,

  projectList: (projectId: string) =>
    [...milestoneQueryKeys.projectLists(), projectId] as const,

  detail: (projectId: string, milestoneId: string) =>
    [...milestoneQueryKeys.all, "detail", projectId, milestoneId] as const,

  create: (projectId: string) =>
    [...milestoneQueryKeys.all, "create", projectId] as const,

  update: (projectId: string, milestoneId: string) =>
    [...milestoneQueryKeys.all, "update", projectId, milestoneId] as const,

  remove: (projectId: string, milestoneId: string) =>
    [...milestoneQueryKeys.all, "delete", projectId, milestoneId] as const,
};
