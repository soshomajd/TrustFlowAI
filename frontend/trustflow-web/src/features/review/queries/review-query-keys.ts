export const reviewQueryKeys = {
  all: ["review"] as const,

  list: (status: "open" | "closed") =>
    [...reviewQueryKeys.all, "list", status] as const,

  detail: (caseId: string) =>
    [...reviewQueryKeys.all, "detail", caseId] as const,
};
