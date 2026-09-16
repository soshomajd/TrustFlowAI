export const freelancerQueryKeys = {
  all: ["freelancer-profiles"] as const,
  mine: (userId: string) => ["freelancer-profiles", "me", userId] as const,
  detail: (userId: string) => ["freelancer-profiles", userId] as const,
};
