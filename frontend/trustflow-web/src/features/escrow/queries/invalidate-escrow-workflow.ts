import type { QueryClient } from "@tanstack/react-query";

import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { notificationQueryKeys } from "@/features/notifications/queries/notification-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";

// A read-only sync can also discover a refund or completion from another tab.
export async function invalidateEscrowWorkflow(queryClient: QueryClient, projectId: string) {
  await Promise.all([
    escrowQueryKeys.participants(projectId),
    escrowQueryKeys.cases(projectId),
    ["escrow-stages", projectId],
    ["escrow-next-plan", projectId],
    projectQueryKeys.all,
    // Delivery originals can unlock when sync discovers a confirmed payout.
    // Refresh submission entitlements as well as the milestone status list.
    milestoneQueryKeys.all,
    proposalQueryKeys.all,
    notificationQueryKeys.all,
  ].map((queryKey) => queryClient.invalidateQueries({ queryKey })));
}
