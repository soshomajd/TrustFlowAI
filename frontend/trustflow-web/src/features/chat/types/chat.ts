export type ChatCounterpart = {
  id: string;
  fullName: string;
  role: "Client" | "Freelancer";
};

export type ChatMessage = {
  id: string;
  mine: boolean;
  body: string;
  sentAt: string;
};

export type ChatConversation = {
  projectId: string;
  projectTitle: string;
  freelancerId: string;
  counterpart: ChatCounterpart;
  canSend: boolean;
  cannotSendReason: string | null;
  messages: ChatMessage[];
};

export type ChatInboxItem = {
  projectId: string;
  projectTitle: string;
  freelancerId: string;
  counterpart: ChatCounterpart;
  lastMessage: string;
  lastMessageMine: boolean;
  lastMessageAt: string;
  unreadCount: number;
};

export type ChatInbox = {
  items: ChatInboxItem[];
  unreadCount: number;
};

export const CHAT_MESSAGE_LIMIT = 2000;
