"use client";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { useAdminConversations, useAdminConversationMessages } from "@/features/admin/hooks/use-admin-conversations";
import type { AdminConversationThread } from "@/features/admin/types/admin-conversations";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
const date = (value: string) => new Date(value).toLocaleString();

export function AdminConversationMessages({ projectId, thread }: { projectId: string; thread: AdminConversationThread }) {
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const [through, setThrough] = useState<string>();
  const [refresh, setRefresh] = useState(0);
  const query = useAdminConversationMessages(projectId, thread.id, cursors[cursors.length - 1], through, refresh);
  const data = query.data?.threadId === thread.id ? query.data : undefined;
  return <section aria-label="Selected conversation" className="space-y-4 rounded-2xl border p-5">
    <header className="flex flex-wrap items-start justify-between gap-3">
      <div><h3 className="text-lg font-semibold">{thread.clientName} and {thread.freelancerName}</h3>
        <p className="mt-1 break-all text-xs text-muted-foreground">Conversation {thread.id}</p></div>
      <Button variant="outline" disabled={query.isFetching} onClick={() => { setThrough(undefined); setCursors([undefined]); setRefresh((old) => old + 1); }}>Refresh latest messages</Button>
    </header>
    <p className="text-sm text-muted-foreground">Read-only conversation history. Messages on each page appear oldest first. New messages appear when you refresh the latest messages.</p>
    {query.isPending && <p role="status">Loading messages…</p>}
    {query.isError && <div role="alert" className="rounded-lg border border-destructive/40 p-4">
      <p>Could not load these messages.</p><p className="mt-2 text-sm">{getApiErrorMessage(query.error, "Try loading the messages again.")}</p>
      {data && <p className="mt-2 text-sm">The messages below are from the last successful refresh.</p>}
      <Button className="mt-3" variant="outline" onClick={() => void query.refetch()}>Retry messages</Button>
    </div>}
    {data && <>
      {data.messages.length === 0 ? <p className="rounded-lg border border-dashed p-4">No saved messages on this page.</p> :
        <ol aria-label="Conversation messages" className="space-y-3">{data.messages.map((message) => <li key={message.id} className="rounded-xl border bg-card/60 p-4">
          <p className="text-sm font-semibold wrap-anywhere">{message.senderName} · {message.senderRole}</p>
          <p className="mt-1 text-xs text-muted-foreground"><time dateTime={message.sentAt}>{date(message.sentAt)}</time></p>
          <p dir="auto" className="mt-3 whitespace-pre-wrap wrap-anywhere text-sm leading-6">{message.body}</p>
          <details className="mt-3 text-xs text-muted-foreground"><summary className="cursor-pointer">Message reference</summary>
            <p className="mt-2 break-all">Message {message.id} · Sender {message.senderId}</p></details>
        </li>)}</ol>}
      <nav aria-label="Message history pages" className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="outline" disabled={!data.hasOlderMessages || !data.nextBeforeMessageId || query.isFetching} onClick={() => {
          setThrough(data.throughMessageId ?? undefined); setCursors((old) => [...old, data.nextBeforeMessageId!]);
        }}>Older messages</Button>
        <p className="text-sm">History page {cursors.length} · {data.messages.length} messages</p>
        <Button variant="outline" disabled={cursors.length === 1 || query.isFetching} onClick={() => setCursors((old) => old.slice(0, -1))}>Newer messages</Button>
      </nav>
    </>}
  </section>;
}

export function AdminConversations({ projectId }: { projectId: string }) {
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string>();
  const query = useAdminConversations(projectId, page);
  const selected = query.data?.items.find((thread) => thread.id === selectedId);
  return <section aria-label="Project conversations" className="space-y-5">
    <header className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-xl font-semibold">Conversations</h2>
      <Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>Refresh conversation list</Button></header>
    <aside className="rounded-xl border p-4 text-sm text-muted-foreground">Conversations are confidential. Administrator views are recorded. Reading here does not change either participant’s unread status. Each freelancer has a separate conversation. Messages are shown as plain text.</aside>
    {query.isPending && <p role="status">Loading conversations…</p>}
    {query.isError && <div role="alert" className="rounded-lg border border-destructive/40 p-4"><p>Could not load conversations.</p>
      <p className="mt-2 text-sm">{getApiErrorMessage(query.error, "Try Refresh conversation list.")}</p>
      {query.data && <p className="mt-2 text-sm">The list below is from the last successful refresh.</p>}</div>}
    {query.data && <>
      <p className="text-sm text-muted-foreground">{query.data.totalItems} saved conversations</p>
      {query.data.items.length === 0 ? <p className="rounded-lg border border-dashed p-5">No saved conversations on this page.</p> :
        <ul aria-label="Saved conversations" className="grid gap-4 lg:grid-cols-2">{query.data.items.map((thread) => <li key={thread.id} className="rounded-xl border bg-card/60 p-5">
          <h3 className="font-semibold wrap-anywhere">{thread.clientName} and {thread.freelancerName}</h3>
          <p className="mt-2 text-sm">{thread.messageCount} saved messages · Last message {thread.messageCount ? date(thread.lastMessageAt) : "Not recorded"}</p>
          <p className="mt-2 break-all text-xs text-muted-foreground">Conversation {thread.id} · Client {thread.clientId ?? "Not recorded"} · Freelancer {thread.freelancerId}</p>
          <Button variant={selectedId === thread.id ? "default" : "outline"} aria-pressed={selectedId === thread.id} className="mt-4" onClick={() => setSelectedId(thread.id)}>View conversation with {thread.freelancerName}</Button>
        </li>)}</ul>}
      <nav aria-label="Conversation list pages" className="flex items-center justify-between gap-3">
        <Button variant="outline" disabled={!query.data.hasPreviousPage || query.isFetching} onClick={() => { setSelectedId(undefined); setPage((old) => old - 1); }}>Previous conversations</Button>
        <p className="text-sm">Page {query.data.page} of {Math.max(1, query.data.totalPages)}</p>
        <Button variant="outline" disabled={!query.data.hasNextPage || query.isFetching} onClick={() => { setSelectedId(undefined); setPage((old) => old + 1); }}>Next conversations</Button>
      </nav>
      {selected ? <AdminConversationMessages key={selected.id} projectId={projectId} thread={selected} /> :
        query.data.items.length > 0 && <p className="text-sm text-muted-foreground">Choose a conversation to inspect its messages.</p>}
    </>}
  </section>;
}
