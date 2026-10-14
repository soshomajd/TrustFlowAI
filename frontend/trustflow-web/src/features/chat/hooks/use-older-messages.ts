"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { getConversationPage } from "@/features/chat/api/chat-api";
import { chatQueryKeys } from "@/features/chat/queries/chat-query-keys";
import type { ChatConversation } from "@/features/chat/types/chat";
import { mergeMessages } from "@/features/chat/lib/merge-messages";

export function useOlderMessages(projectId: string, freelancerId: string, beforePrepend: () => void) {
  const queryClient = useQueryClient();
  const key = chatQueryKeys.conversation(projectId, freelancerId);

  return useMutation({
    mutationFn: (beforeMessageId: string) =>
      getConversationPage(projectId, freelancerId, { beforeMessageId }),
    onSuccess: (page) => {
      beforePrepend();
      queryClient.setQueryData<ChatConversation>(key, (current) => {
        if (!current) return page;
        return {
          ...current,
          hasOlderMessages: page.hasOlderMessages,
          messages: mergeMessages(page.messages, current.messages),
        };
      });
    },
  });
}
