export const aiQueryKeys = {
  all: ["ai"] as const,

  status: () => [...aiQueryKeys.all, "status"] as const,

  review: (projectId: string, milestoneId: string) =>
    [...aiQueryKeys.all, "review", projectId, milestoneId] as const,

  disputes: (projectId: string, milestoneId: string) =>
    [...aiQueryKeys.all, "disputes", projectId, milestoneId] as const,

  draftProject: () => [...aiQueryKeys.all, "draft-project"] as const,

  coachProfile: () => [...aiQueryKeys.all, "coach-profile"] as const,

  draftResume: () => [...aiQueryKeys.all, "draft-resume"] as const,
};
