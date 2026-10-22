import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { registerHooks } from "node:module";
import { test } from "node:test";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
const moduleUrl = (source) => `data:text/javascript,${encodeURIComponent(source)}`;
registerHooks({
  resolve(specifier, context, nextResolve) {
    if (specifier === "@/features/admin/hooks/use-admin-finance") return { url: moduleUrl('export function useAdminFinance(){return {};} export function useAdminFinanceEntries(){return {};}'), shortCircuit: true };
    if (specifier === "@/features/admin/api/admin-finance-api") return { url: moduleUrl('export async function downloadAdminFinanceReport(){throw new Error("No network during render");}'), shortCircuit: true };
    if (specifier === "@/features/admin/hooks/use-admin-conversations") return { url: moduleUrl('export function useAdminConversations(){return globalThis.adminConversations;} export function useAdminConversationMessages(){globalThis.messageQueryCalls++;return globalThis.adminMessages;}'), shortCircuit: true };
    if (specifier === "next/link") return nextResolve("next/link.js", context);
    if (specifier === "@/features/admin/hooks/use-admin-dossier") return { url: moduleUrl('export function useAdminDossier(){return globalThis.dossierProject;} export function useAdminDossierRecords(){return globalThis.dossierRecords;}'), shortCircuit: true };
    if (specifier === "@/features/admin/api/admin-dossier-api") return { url: moduleUrl('export async function downloadAdminDeliveryFile(){throw new Error("No network during render");}'), shortCircuit: true };
    if (specifier.startsWith("@/")) {
      const base = new URL(`../src/${specifier.slice(2)}`, import.meta.url);
      for (const extension of [".ts", ".tsx"]) if (existsSync(fileURLToPath(base.href + extension))) return nextResolve(base.href + extension, context);
    }
    return nextResolve(specifier, context);
  },
  load(url, context, nextLoad) {
    if (url.endsWith(".tsx")) return { format: "module", shortCircuit: true,
      source: ts.transpileModule(readFileSync(new URL(url), "utf8"), { compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText };
    return nextLoad(url, context);
  },
});
const { AdminConversations, AdminConversationMessages } = await import("../src/features/admin/components/admin-conversations.tsx");
const base = { isPending: false, isFetching: false, isError: false, refetch(){} };
const thread = { id: "thread-one", clientId: "client", clientName: "Sara <client>", freelancerId: "freelancer", freelancerName: "Reza <freelancer>", createdAt: "2026-10-19T12:00:00Z", lastMessageAt: "2026-10-19T12:00:00Z", messageCount: 420 };
const message = { id: "message-one", senderId: "freelancer", senderName: "Reza <freelancer>", senderRole: "Freelancer", body: "Stored <script>alert(1)</script>\nhttps://example.com/a", sentAt: thread.lastMessageAt };
const page = (items) => ({ items, totalItems: items.length, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false });
const history = { threadId: thread.id, throughMessageId: message.id, hasOlderMessages: true, nextBeforeMessageId: message.id, messages: [message] };
function list(state = { data: page([thread]) }) {
  globalThis.adminConversations = { ...base, ...state }; globalThis.messageQueryCalls = 0;
  return renderToStaticMarkup(React.createElement(AdminConversations, { projectId: "project-one" }));
}
function messages(state = { data: history }) {
  globalThis.adminMessages = { ...base, ...state }; globalThis.messageQueryCalls = 0;
  return renderToStaticMarkup(React.createElement(AdminConversationMessages, { projectId: "project-one", thread }));
}
test("thread list exposes only metadata until the administrator selects a conversation", () => {
  const html = list();
  for (const text of ["Sara &lt;client&gt;", "Reza &lt;freelancer&gt;", "420 saved messages", "Views", "Choose a conversation", "Each freelancer has a separate conversation", "unread status"])
    assert.ok(html.toLowerCase().includes(text.toLowerCase()), text);
  assert.equal(globalThis.messageQueryCalls, 0);
  assert.doesNotMatch(html, /Stored|alert\(1\)|Send message|<textarea|Download conversation/);
});
test("message history keeps author, role, time and multiline plain text without active links", () => {
  const html = messages();
  for (const text of ["Reza &lt;freelancer&gt;", "Freelancer", "Stored &lt;script&gt;alert(1)&lt;/script&gt;", "https://example.com/a", "Message reference", "message-one", "Older messages", "Newer messages", "History page 1", "Refresh latest messages"])
    assert.ok(html.includes(text), text);
  assert.match(html, /dir="auto"/); assert.match(html, /<time dateTime="2026-10-19T12:00:00Z"/);
  assert.doesNotMatch(html, /<script>|<iframe|<textarea|<input|href="https:|Send message|Edit message|Delete message/);
});
test("loading and empty histories never fabricate messages or an author", () => {
  assert.match(list({ isPending: true }), /Loading conversations/);
  assert.doesNotMatch(list({ data: page([]) }), /Reza|Selected conversation/);
  assert.match(list({ data: page([]) }), /No saved conversations/);
  assert.match(messages({ isPending: true }), /Loading messages/);
  const html = messages({ data: { ...history, hasOlderMessages: false, nextBeforeMessageId: null, messages: [] } });
  assert.match(html, /No saved messages on this page/); assert.match(html, /0 messages/);
});
test("errors distinguish stale saved text from a successful refresh and offer retry", () => {
  const html = messages({ data: history, isError: true, error: new Error("Offline") });
  assert.match(html, /last successful refresh/); assert.match(html, /Retry messages/); assert.match(html, /Could not load these messages/);
  assert.match(list({ data: page([thread]), isError: true, error: new Error("Offline") }), /last successful refresh/);
});
test("messages from a different selected thread never render under this participant pair", () => {
  const html = messages({ data: { ...history, threadId: "other-thread", messages: [{ ...message, body: "Wrong conversation secret" }] } });
  assert.doesNotMatch(html, /Wrong conversation secret|Stored/);
});
test("separate freelancer cards and missing clients remain honest", () => {
  const html = list({ data: page([thread, { ...thread, id: "second-thread", freelancerName: "Other freelancer", clientId: null, clientName: "Former client", messageCount: 0 }]) });
  assert.match(html, /Other freelancer/); assert.match(html, /Former client/); assert.match(html, /0 saved messages/); assert.match(html, /Last message Not recorded/);
});
