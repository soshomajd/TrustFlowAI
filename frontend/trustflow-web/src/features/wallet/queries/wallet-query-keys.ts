export const walletQueryKeys = {
  all: ["wallet"] as const,

  current: () => [...walletQueryKeys.all, "current"] as const,

  connect: () => [...walletQueryKeys.all, "connect"] as const,
};
