export type AdminConversationThread = {
  id: string; clientId: string | null; clientName: string; freelancerId: string; freelancerName: string;
  createdAt: string; lastMessageAt: string; messageCount: number;
};
export type AdminConversationThreads = {
  items: AdminConversationThread[]; page: number; pageSize: number; totalItems: number; totalPages: number;
  hasPreviousPage: boolean; hasNextPage: boolean;
};
export type AdminConversationMessage = {
  id: string; senderId: string; senderName: string; senderRole: string; body: string; sentAt: string;
};
export type AdminConversationMessages = {
  threadId: string; throughMessageId: string | null; hasOlderMessages: boolean;
  nextBeforeMessageId: string | null; messages: AdminConversationMessage[];
};
