"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";

import { getConversation } from "@/features/chat/api/chat-api";
import { chatQueryKeys } from "@/features/chat/queries/chat-query-keys";
import type { ChatConversation } from "@/features/chat/types/chat";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

// Opens with the latest messages, then asks only for what is newer than the
// last one it has and adds that to the list. Opening a conversation marks it
// read on the server, so the badge clears by itself.
export function useConversation(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();
  const key = chatQueryKeys.conversation(projectId, freelancerId);

  return useQuery({
    queryKey: key,

    queryFn: async ({ signal }) => {
      const previous = queryClient.getQueryData<ChatConversation>(key);
      const last = previous?.messages.at(-1)?.sentAt;

      const fresh = await getConversation(
        projectId,
        freelancerId,
        last,
        signal,
      );

      if (!previous || !last) {
        return fresh;
      }

      const known = new Set(previous.messages.map((message) => message.id));

      return {
        ...fresh,
        messages: [
          ...previous.messages,
          ...fresh.messages.filter((message) => !known.has(message.id)),
        ],
      };
    },

    enabled: isValidGuid(projectId) && isValidGuid(freelancerId),

    staleTime: 0,
    refetchInterval: 5_000,
    refetchOnWindowFocus: true,

    // A conversation that does not exist (or is not yours) is a plain 404,
    // not something to retry.
    retry: false,
  });
}
