import "client-only";

import type {
  ChatConversation,
  ChatInbox,
  ChatMessage,
} from "@/features/chat/types/chat";
import { apiClient } from "@/lib/api/api-client";

export async function getChatInbox(signal?: AbortSignal) {
  const response = await apiClient.get<ChatInbox>("/chat", { signal });

  return response.data;
}

// With `after`, only messages sent later than that time come back, so a poll
// of a quiet conversation is almost empty.
export async function getConversation(
  projectId: string,
  freelancerId: string,
  after?: string,
  signal?: AbortSignal,
) {
  const response = await apiClient.get<ChatConversation>(
    `/projects/${projectId}/chat/${freelancerId}`,
    { params: after ? { after } : undefined, signal },
  );

  return response.data;
}

export async function sendChatMessage(
  projectId: string,
  freelancerId: string,
  body: string,
) {
  const response = await apiClient.post<ChatMessage>(
    `/projects/${projectId}/chat/${freelancerId}/messages`,
    { body },
  );

  return response.data;
}
