"use client";
import { useQuery } from "@tanstack/react-query";
import { getAdminConversations, getAdminConversationMessages } from "@/features/admin/api/admin-conversations-api";
import { useAuthStore } from "@/stores/auth-store";
export function useAdminConversations(projectId: string, page: number) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "conversations", user?.id, projectId, page],
    queryFn: ({ signal }) => getAdminConversations(projectId, page, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
export function useAdminConversationMessages(projectId: string, threadId: string, beforeMessageId?: string, throughMessageId?: string, refresh = 0) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "conversation-messages", user?.id, projectId, threadId, beforeMessageId, throughMessageId, refresh],
    queryFn: ({ signal }) => getAdminConversationMessages(projectId, threadId, beforeMessageId, throughMessageId, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
