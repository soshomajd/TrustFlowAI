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
    if (specifier === "@/features/admin/hooks/use-admin-timeline") return { url: moduleUrl('export function useAdminTimeline(){return globalThis.timelineQuery;}'), shortCircuit: true };
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
const { AdminTimeline, AdminTimelineEvents } = await import("../src/features/admin/components/admin-timeline.tsx");
const at = "2026-10-18T12:00:00Z";
const row = { code: 51, resourceId: "message-reference", relatedId: "thread-reference", category: "conversations", title: "Message sent",
  label: "<script>plain text</script>\nhttps://example.com", occurredAt: at, actorId: "historical-author", actorName: "Reza <author>", transactionHash: null };
const page = { projectId: "project-one", snapshotAt: at, items: [row], nextCursor: "next-protected-page" };
const base = { isPending: false, isFetching: false, isError: false, refetch(){} };
function screen(state = { data: page }) {
  globalThis.timelineQuery = { ...base, ...state };
  return renderToStaticMarkup(React.createElement(AdminTimeline, { projectId: "project-one", onInspect(){} }));
}
function events(items, projectId = "project-one") {
  return renderToStaticMarkup(React.createElement(AdminTimelineEvents, { data: { ...page, items }, projectId, onInspect(){}, onActor(){} }));
}
test("history exposes categories, date filters, explicit cutoff, paging and read-only privacy notice", () => {
  const html = screen();
  for (const text of ["Timeline and admin access", "Administrator access", "Conversation activity", "Disputes and AI opinions", "Recorded times", "Missing times", "From (UTC)", "Through (UTC)", "Apply filters", "Refresh latest events", "Newer events", "Older events", "access cutoff", "mark participant messages read", "AI opinions are advice"])
    assert.ok(html.includes(text), text);
  assert.doesNotMatch(html, /<textarea|type="password"|Connect wallet|Release payment|Delete event/);
});
test("dated metadata keeps references and author without active links or HTML execution", () => {
  const html = events([row]);
  for (const text of ["Reza &lt;author&gt;", "message-reference", "thread-reference", "Filter this actor", "Open related section", "&lt;script&gt;", "https://example.com", `dateTime="${at}"`]) assert.ok(html.includes(text), text);
  assert.doesNotMatch(html, /<script>|<iframe|href="https:/);
});
test("missing timestamps and actors stay unknown instead of borrowing dates", () => {
  const html = events([{ ...row, occurredAt: null, actorName: null, actorId: null, transactionHash: "saved-transaction" }]);
  assert.match(html, /Time not recorded/); assert.match(html, /not placed in the dated sequence/);
  assert.match(html, /Not recorded; no actor inferred/); assert.match(html, /saved-transaction/);
  assert.doesNotMatch(html, /<time|Filter this actor/);
});
test("foreign project metadata is never displayed", () => {
  const html = events([{ ...row, label: "FOREIGN PRIVATE DATA" }], "other-project");
  assert.match(html, /another project/); assert.doesNotMatch(html, /FOREIGN PRIVATE DATA|message-reference/);
});
test("empty, loading and errors remain distinct with a refresh action", () => {
  assert.match(screen({ isPending: true }), /Loading project history/);
  assert.match(screen({ data: { ...page, items: [], nextCursor: null } }), /No saved events match/);
  const html = screen({ data: page, isError: true, error: new Error("offline") });
  assert.match(html, /Could not load project history/); assert.match(html, /last successful refresh/); assert.match(html, /Retry history/);
});
