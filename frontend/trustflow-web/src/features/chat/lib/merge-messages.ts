import type { ChatMessage } from "@/features/chat/types/chat";

export function mergeMessages(first: ChatMessage[], second: ChatMessage[]) {
  const messages = new Map(first.map((message) => [message.id, message]));
  for (const message of second) messages.set(message.id, message);
  return [...messages.values()].sort((left, right) =>
    left.sentAt.localeCompare(right.sentAt) || left.id.localeCompare(right.id),
  );
}
