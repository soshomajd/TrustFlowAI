export type TimelineCategory = "all" | "project" | "proposals" | "milestones" | "escrows" | "deliveries" | "conversations" | "disputes" | "admin";
export type TimelineFilters = { category: TimelineCategory; timing: "recorded" | "missing"; from?: string; to?: string; actorId?: string };
export type TimelineEvent = {
  code: number; resourceId: string; relatedId: string | null; category: TimelineCategory;
  title: string; label: string | null; occurredAt: string | null;
  actorId: string | null; actorName: string | null; transactionHash: string | null;
  transactionSender?: string | null; blockNumber?: number | null;
};
export type TimelinePage = { projectId: string; snapshotAt: string; items: TimelineEvent[]; nextCursor: string | null };
