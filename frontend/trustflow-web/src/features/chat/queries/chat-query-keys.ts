export const chatQueryKeys = {
  all: ["chat"] as const,

  inbox: () => [...chatQueryKeys.all, "inbox"] as const,

  conversation: (projectId: string, freelancerId: string) =>
    [...chatQueryKeys.all, "conversation", projectId, freelancerId] as const,
};
