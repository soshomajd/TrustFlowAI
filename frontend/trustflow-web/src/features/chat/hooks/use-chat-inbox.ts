"use client";

import { useQuery } from "@tanstack/react-query";

import { getChatInbox } from "@/features/chat/api/chat-api";
import { chatQueryKeys } from "@/features/chat/queries/chat-query-keys";
import { useAuthStore } from "@/stores/auth-store";

export function useChatInbox() {
  const isAuthenticated = useAuthStore((state) => Boolean(state.user));

  return useQuery({
    queryKey: chatQueryKeys.inbox(),
    queryFn: ({ signal }) => getChatInbox(signal),
    enabled: isAuthenticated,
    staleTime: 10_000,
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
  });
}
