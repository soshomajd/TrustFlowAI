"use client";

import { ArrowLeft, LoaderCircle, Send, ShieldCheck } from "lucide-react";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";

import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { useConversation } from "@/features/chat/hooks/use-conversation";
import { useSendMessage } from "@/features/chat/hooks/use-send-message";
import { useOlderMessages } from "@/features/chat/hooks/use-older-messages";
import {
  CHAT_MESSAGE_LIMIT,
  type ChatMessage,
} from "@/features/chat/types/chat";
import { MemberName } from "@/features/profile-photos/components/user-avatar";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";

const timeFormat = new Intl.DateTimeFormat(undefined, {
  hour: "2-digit",
  minute: "2-digit",
});

const dayFormat = new Intl.DateTimeFormat(undefined, {
  weekday: "long",
  month: "long",
  day: "numeric",
});

// How far from the bottom still counts as "reading the latest".
const STICK_DISTANCE_PX = 120;

export function ChatConversationScreen({
  projectId,
  freelancerId,
}: {
  projectId: string;
  freelancerId: string;
}) {
  const query = useConversation(projectId, freelancerId);
  const send = useSendMessage(projectId, freelancerId);

  const [draft, setDraft] = useState("");
  const [problem, setProblem] = useState<string | null>(null);

  const listRef = useRef<HTMLDivElement>(null);
  const stickToBottom = useRef(true);
  const olderAnchor = useRef<{ height: number; top: number } | null>(null);
  const older = useOlderMessages(projectId, freelancerId, () => {
    const list = listRef.current;
    if (list) olderAnchor.current = { height: list.scrollHeight, top: list.scrollTop };
  });

  const messageCount = query.data?.messages.length ?? 0;

  // Keeps the newest message in view, unless the reader scrolled up to look
  // at something older.
  useEffect(() => {
    const list = listRef.current;

    if (list && olderAnchor.current) {
      list.scrollTop = olderAnchor.current.top + list.scrollHeight - olderAnchor.current.height;
      olderAnchor.current = null;
    } else if (list && stickToBottom.current) {
      list.scrollTop = list.scrollHeight;
    }
  }, [messageCount, older.isPending]);

  function handleScroll() {
    const list = listRef.current;

    if (list) {
      stickToBottom.current =
        list.scrollHeight - list.scrollTop - list.clientHeight <
        STICK_DISTANCE_PX;
    }
  }

  function submit() {
    const body = draft.trim();

    if (!body || send.isPending) {
      return;
    }

    setProblem(null);

    send.mutate(body, {
      onSuccess: () => {
        stickToBottom.current = true;
        setDraft("");
      },

      // The text stays in the box, so a refused message can be edited
      // instead of retyped.
      onError: (error) =>
        setProblem(
          getApiErrorMessage(error, "The message could not be sent."),
        ),
    });
  }

  function loadOlder() {
    const first = query.data?.messages[0]?.id;
    if (!first || older.isPending) return;
    stickToBottom.current = false;
    older.mutate(first);
  }

  if (query.isPending) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <LoaderCircle className="size-4 animate-spin" aria-hidden="true" />
          Loading the conversation
        </p>
      </main>
    );
  }

  if (query.isError || !query.data) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-6">
        <BackLink />

        <p role="alert" className="mt-6 text-sm text-destructive">
          {getApiErrorMessage(
            query.error,
            "This conversation could not be opened.",
          )}
        </p>
      </main>
    );
  }

  const conversation = query.data;

  return (
    <main className="mx-auto max-w-3xl px-4 py-6 sm:px-6">
      <BackLink />

      <section
        aria-label={`Conversation with ${conversation.counterpart.fullName}`}
        className="mt-4 flex flex-col overflow-hidden rounded-2xl border bg-card/70"
      >
        <header className="border-b p-4">
          <h1 className="text-lg font-semibold">
            <MemberName
              userId={conversation.counterpart.id}
              name={conversation.counterpart.fullName}
            />
          </h1>

          <p className="mt-1 text-sm text-muted-foreground">
            {conversation.counterpart.role} on {conversation.projectTitle}
          </p>
        </header>

        <div
          ref={listRef}
          onScroll={handleScroll}
          role="log"
          aria-live="polite"
          aria-label="Messages"
          className="h-[min(26rem,55dvh)] space-y-1 overflow-y-auto p-4"
        >
          {conversation.hasOlderMessages && (
            <div className="mb-4 text-center">
              <Button type="button" variant="outline" size="sm" disabled={older.isPending} onClick={loadOlder}>
                {older.isPending ? "Loading older messages…" : "Load older messages"}
              </Button>
              {older.isError && (
                <p role="alert" className="mt-2 text-sm text-destructive">
                  {getApiErrorMessage(older.error, "Older messages could not be loaded. Try again.")}
                </p>
              )}
            </div>
          )}
          {messageCount === 0 && (
            <p className="py-10 text-center text-sm text-muted-foreground">
              No messages yet.
              {conversation.canSend && " Say hello and ask your first question."}
            </p>
          )}

          {conversation.messages.map((message, index) => (
            <MessageRow
              key={message.id}
              message={message}
              previous={conversation.messages[index - 1]}
            />
          ))}
        </div>

        <div className="border-t p-4">
          <p className="flex items-start gap-2 text-xs leading-5 text-muted-foreground">
            <ShieldCheck
              className="mt-0.5 size-4 shrink-0 text-electric"
              aria-hidden="true"
            />
            Keep it on TrustFlow. Phone numbers, email addresses, links and
            messenger accounts cannot be sent here, so the escrow can protect
            you both. Send files as a delivery.
          </p>

          {conversation.canSend ? (
            <form
              className="mt-3"
              onSubmit={(event) => {
                event.preventDefault();
                submit();
              }}
            >
              <label htmlFor="chat-message" className="sr-only">
                Your message
              </label>

              <Textarea
                id="chat-message"
                value={draft}
                maxLength={CHAT_MESSAGE_LIMIT}
                rows={3}
                placeholder="Write a message"
                aria-invalid={problem ? true : undefined}
                aria-describedby={problem ? "chat-problem" : undefined}
                onChange={(event) => {
                  setDraft(event.target.value);
                  setProblem(null);
                }}
                onKeyDown={(event) => {
                  // Enter sends; Shift+Enter starts a new line.
                  if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    submit();
                  }
                }}
              />

              {problem && (
                <p
                  id="chat-problem"
                  role="alert"
                  className="mt-2 text-sm leading-6 text-destructive"
                >
                  {problem}
                </p>
              )}

              <div className="mt-3 flex items-center justify-between gap-3">
                <span className="text-xs tabular-nums text-muted-foreground">
                  {draft.length} / {CHAT_MESSAGE_LIMIT}
                </span>

                <Button
                  type="submit"
                  disabled={!draft.trim() || send.isPending}
                >
                  {send.isPending ? (
                    <LoaderCircle
                      className="size-4 animate-spin"
                      aria-hidden="true"
                    />
                  ) : (
                    <Send className="size-4" aria-hidden="true" />
                  )}
                  Send
                </Button>
              </div>
            </form>
          ) : (
            <p
              role="status"
              className="mt-3 rounded-lg border border-dashed p-3 text-sm text-muted-foreground"
            >
              {conversation.cannotSendReason ??
                "You cannot send messages here."}
            </p>
          )}
        </div>
      </section>
    </main>
  );
}

function BackLink() {
  return (
    <Link
      href="/dashboard/messages"
      className="inline-flex min-h-11 items-center gap-2 rounded-lg text-sm text-muted-foreground hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none"
    >
      <ArrowLeft className="size-4" aria-hidden="true" />
      All messages
    </Link>
  );
}

function MessageRow({
  message,
  previous,
}: {
  message: ChatMessage;
  previous?: ChatMessage;
}) {
  const sentAt = new Date(message.sentAt);
  const newDay =
    !previous ||
    new Date(previous.sentAt).toDateString() !== sentAt.toDateString();

  return (
    <>
      {newDay && (
        <p className="py-3 text-center text-xs text-muted-foreground">
          {dayFormat.format(sentAt)}
        </p>
      )}

      <div className={cn("flex", message.mine ? "justify-end" : "justify-start")}>
        <div
          className={cn(
            "max-w-[85%] rounded-2xl px-3.5 py-2 text-sm leading-6",
            message.mine
              ? "rounded-br-md bg-primary text-primary-foreground"
              : "rounded-bl-md border bg-background/60",
          )}
        >
          <p className="whitespace-pre-wrap break-words">{message.body}</p>

          <time
            dateTime={message.sentAt}
            className={cn(
              "mt-0.5 block text-right text-xs",
              message.mine
                ? "text-primary-foreground/80"
                : "text-muted-foreground",
            )}
          >
            {timeFormat.format(sentAt)}
          </time>
        </div>
      </div>
    </>
  );
}
