"use client";

import { LoaderCircle, MessageSquare } from "lucide-react";
import Link from "next/link";

import { useChatInbox } from "@/features/chat/hooks/use-chat-inbox";
import type { ChatInboxItem } from "@/features/chat/types/chat";
import { UserAvatar } from "@/features/profile-photos/components/user-avatar";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";

const timeFormat = new Intl.DateTimeFormat(undefined, {
  month: "short",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

export function ChatInboxScreen() {
  const query = useChatInbox();

  return (
    <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
      <header className="flex items-start gap-3">
        <MessageSquare className="mt-1 size-6 text-electric" aria-hidden="true" />

        <div>
          <h1 className="text-2xl font-semibold">Messages</h1>

          <p className="mt-2 text-sm leading-6 text-muted-foreground">
            Talk to the client or freelancer you are working with. Each
            conversation belongs to one project.
          </p>
        </div>
      </header>

      {query.isPending && (
        <p className="mt-8 flex items-center gap-2 text-sm text-muted-foreground">
          <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
          Loading messages
        </p>
      )}

      {query.isError && (
        <p role="alert" className="mt-8 text-sm text-destructive">
          {getApiErrorMessage(query.error, "Messages could not be loaded.")}
        </p>
      )}

      {query.data && query.data.items.length === 0 && (
        <div className="mt-8 rounded-xl border border-dashed p-8 text-center text-sm leading-6 text-muted-foreground">
          <p className="font-medium text-foreground">No conversations yet.</p>

          <p className="mt-1">
            Clients can message a freelancer from their proposal. Freelancers
            can message the client once they have sent a proposal.
          </p>
        </div>
      )}

      <ul className="mt-6 space-y-2">
        {query.data?.items.map((item) => (
          <li key={`${item.projectId}:${item.freelancerId}`}>
            <InboxRow item={item} />
          </li>
        ))}
      </ul>
    </main>
  );
}

function InboxRow({ item }: { item: ChatInboxItem }) {
  const unread = item.unreadCount > 0;

  return (
    <Link
      href={`/dashboard/messages/${item.projectId}/${item.freelancerId}`}
      className={cn(
        "flex min-h-16 items-center gap-3 rounded-xl border bg-card/70 p-3 transition-colors hover:border-primary/40 focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none sm:p-4",
        unread && "border-primary/40",
      )}
    >
      <UserAvatar
        userId={item.counterpart.id}
        name={item.counterpart.fullName}
        className="size-11 shrink-0"
      />

      <div className="min-w-0 flex-1">
        <div className="flex items-baseline justify-between gap-3">
          <p className={cn("truncate", unread ? "font-semibold" : "font-medium")}>
            {item.counterpart.fullName}
          </p>

          <time
            dateTime={item.lastMessageAt}
            className="shrink-0 text-xs text-muted-foreground"
          >
            {timeFormat.format(new Date(item.lastMessageAt))}
          </time>
        </div>

        <p className="truncate text-xs text-muted-foreground">
          {item.projectTitle}
        </p>

        <p
          className={cn(
            "mt-1 truncate text-sm",
            unread ? "text-foreground" : "text-muted-foreground",
          )}
        >
          {item.lastMessageMine && "You: "}
          {item.lastMessage}
        </p>
      </div>

      {unread && (
        <span className="flex h-6 min-w-6 shrink-0 items-center justify-center rounded-full bg-primary px-2 text-xs font-semibold text-primary-foreground">
          <span className="sr-only">Unread messages: </span>
          {item.unreadCount}
        </span>
      )}
    </Link>
  );
}
