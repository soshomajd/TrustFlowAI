export const escrowQueryKeys = {
  all: ["escrow"] as const,

  // One query per project holding every assigned freelancer's escrow, so a
  // multi-freelancer project stays in a single cache entry.
  participants: (projectId: string) =>
    [...escrowQueryKeys.all, "participants", projectId] as const,

  create: (projectId: string, freelancerId: string) =>
    [...escrowQueryKeys.all, "create", projectId, freelancerId] as const,

  deploy: (projectId: string, freelancerId: string) =>
    [...escrowQueryKeys.all, "deploy", projectId, freelancerId] as const,

  sync: (projectId: string, freelancerId: string) =>
    [...escrowQueryKeys.all, "sync", projectId, freelancerId] as const,

  fund: (projectId: string, freelancerId: string) =>
    [...escrowQueryKeys.all, "fund", projectId, freelancerId] as const,

  startWork: (projectId: string, freelancerId: string) =>
    [...escrowQueryKeys.all, "start-work", projectId, freelancerId] as const,

  cancel: (projectId: string, freelancerId: string) =>
    [...escrowQueryKeys.all, "cancel", projectId, freelancerId] as const,
};
