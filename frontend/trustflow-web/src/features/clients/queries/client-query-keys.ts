export const clientQueryKeys = {
  all: ["client-profiles"] as const,
  mine: (userId: string) => ["client-profiles", "me", userId] as const,
  detail: (userId: string) => ["client-profiles", userId] as const,
  verificationQueue: ["client-verifications", "pending"] as const,
};
