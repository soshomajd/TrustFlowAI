import "client-only";
import { apiClient } from "@/lib/api/api-client";
import type { AdminConversationThreads, AdminConversationMessages } from "@/features/admin/types/admin-conversations";
export async function getAdminConversations(projectId: string, page: number, signal?: AbortSignal) {
  return (await apiClient.get<AdminConversationThreads>(`/admin/projects/${encodeURIComponent(projectId)}/conversations`,
    { params: { page, pageSize: 20 }, signal })).data;
}
export async function getAdminConversationMessages(projectId: string, threadId: string, beforeMessageId?: string, throughMessageId?: string, signal?: AbortSignal) {
  return (await apiClient.get<AdminConversationMessages>(`/admin/projects/${encodeURIComponent(projectId)}/conversations/${encodeURIComponent(threadId)}/messages`,
    { params: { beforeMessageId, throughMessageId, pageSize: 50 }, signal })).data;
}
