export const escrowQueryKeys = {
  all: ["escrow"] as const,

  detail: (projectId: string) =>
    [...escrowQueryKeys.all, "detail", projectId] as const,

  create: (projectId: string) =>
    [...escrowQueryKeys.all, "create", projectId] as const,

  deploy: (projectId: string) =>
    [...escrowQueryKeys.all, "deploy", projectId] as const,

  sync: (projectId: string) =>
    [...escrowQueryKeys.all, "sync", projectId] as const,

  fund: (projectId: string) =>
    [...escrowQueryKeys.all, "fund", projectId] as const,

  startWork: (projectId: string) =>
    [...escrowQueryKeys.all, "start-work", projectId] as const,

  cancel: (projectId: string) =>
    [...escrowQueryKeys.all, "cancel", projectId] as const,
};
