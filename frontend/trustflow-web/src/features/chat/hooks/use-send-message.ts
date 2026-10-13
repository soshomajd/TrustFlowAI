"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";

import { sendChatMessage } from "@/features/chat/api/chat-api";
import { chatQueryKeys } from "@/features/chat/queries/chat-query-keys";
import type { ChatConversation } from "@/features/chat/types/chat";
import { notificationQueryKeys } from "@/features/notifications/queries/notification-query-keys";

export function useSendMessage(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: string) =>
      sendChatMessage(projectId, freelancerId, body),

    onSuccess: (message) => {
      queryClient.setQueryData<ChatConversation>(
        chatQueryKeys.conversation(projectId, freelancerId),
        (current) =>
          current &&
          !current.messages.some((item) => item.id === message.id)
            ? { ...current, messages: [...current.messages, message] }
            : current,
      );

      void queryClient.invalidateQueries({ queryKey: chatQueryKeys.inbox() });
      void queryClient.invalidateQueries({
        queryKey: notificationQueryKeys.all,
      });
    },
  });
}
