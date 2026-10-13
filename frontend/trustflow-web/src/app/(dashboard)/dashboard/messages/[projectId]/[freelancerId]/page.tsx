import type { Metadata } from "next";

import { ChatConversationScreen } from "@/features/chat/components/chat-conversation-screen";

export const metadata: Metadata = {
    title: "Conversation",
};

type ConversationPageProps = {
    params: Promise<{
        projectId: string;
        freelancerId: string;
    }>;
};

export default async function ConversationPage({
    params,
}: ConversationPageProps) {
    const { projectId, freelancerId } = await params;

    return (
        <ChatConversationScreen
            projectId={projectId}
            freelancerId={freelancerId}
        />
    );
}
