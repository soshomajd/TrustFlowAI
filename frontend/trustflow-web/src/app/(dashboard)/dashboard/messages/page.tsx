import type { Metadata } from "next";

import { ChatInboxScreen } from "@/features/chat/components/chat-inbox-screen";

export const metadata: Metadata = {
    title: "Messages",
};

export default function MessagesPage() {
    return <ChatInboxScreen />;
}
