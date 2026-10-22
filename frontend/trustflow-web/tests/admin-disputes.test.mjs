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
    if (specifier === "@/features/admin/hooks/use-admin-conversations") return { url: moduleUrl('export function useAdminConversations(){return {};} export function useAdminConversationMessages(){return {};}'), shortCircuit: true };
    if (specifier === "@/features/admin/hooks/use-admin-disputes") return { url: moduleUrl('export function useAdminDisputes(){return globalThis.adminDisputes;} export function useAdminDispute(){globalThis.disputeDetailCalls++;return globalThis.adminDispute;}'), shortCircuit: true };
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
const { AdminDisputes, AdminDisputeDetails } = await import("../src/features/admin/components/admin-disputes.tsx");
const base = { isPending: false, isFetching: false, isError: false, refetch(){} };
const record = { id: "case-one", recordType: "escrow", milestoneId: "stage-one", milestoneTitle: "Stage <one>", milestoneAmountExact: "9007199254740991.01", status: "Open", openedByRole: "Freelancer", clientId: "client", clientName: "Sara", freelancerId: "talent", freelancerName: "Original talent", openedAt: "2026-10-01T12:00:00Z", closedAt: null, reviewableAt: "2026-10-15T12:00:00Z", fallbackAt: "2026-12-30T12:00:00Z", caseKind: "PaymentRequest" };
const detail = { record, escrowId: "escrow", chainId: 31337, contractAddress: "contract", tokenAddress: "token", arbiterWalletAddress: "immutable-arbiter", clientWalletAddress: "client-wallet", freelancerWalletAddress: "talent-wallet", openerStatement: null, respondentStatement: null, respondentStatementAt: null, reviewNote: "Saved <b>note</b>", reviewNoteAt: null, outcome: null, opinion: null };
const page = (items) => ({ items, page: 1, totalPages: 1, totalItems: items.length, hasNextPage: false, hasPreviousPage: false });
function list(state = { data: page([record]) }) { globalThis.adminDisputes = { ...base, ...state }; globalThis.disputeDetailCalls = 0; return renderToStaticMarkup(React.createElement(AdminDisputes, { projectId: "project-one" })); }
function selected(state = { data: detail }, kind = "escrow") { globalThis.adminDispute = { ...base, ...state }; globalThis.disputeDetailCalls = 0; return renderToStaticMarkup(React.createElement(AdminDisputeDetails, { projectId: "project-one", kind, id: record.id })); }
test("dispute list separates financial cases and written opinions without loading statements", () => {
  const html = list();
  for (const text of ["Escrow cases", "Written disagreements", "Their statuses do not mean the same thing", "Administrator access is recorded", "9,007,199,254,740,991.01", "Original talent"]) assert.ok(html.includes(text), text);
  assert.equal(globalThis.disputeDetailCalls, 0); assert.doesNotMatch(html, /Saved &lt;b&gt;note/);
});
test("case details label recorded deadlines and immutable contract identities without settlement controls", () => {
  const html = selected();
  for (const text of ["Arbiter review from (14 days)", "Silence fallback from (90 days)", "does not confirm current on-chain eligibility", "immutable-arbiter", "PaymentRequest", "Saved &lt;b&gt;note&lt;/b&gt;", "No settled financial outcome", "No direct case-to-statement link"]) assert.ok(html.includes(text), text);
  assert.match(html, /href="\/dashboard\/review\/case-one"/);
  assert.doesNotMatch(html, /<textarea|<input|Settle case<\/button>|Request AI|<b>note/);
});
test("written statements keep saved authors and never imply AI paid or settled a contract", () => {
  const html = selected({ data: { ...detail, record: { ...record, recordType: "written", status: "Ruled" }, openerStatement: "Original <script>statement</script>", respondentStatement: null,
    opinion: { readable: true, summary: "Saved <opinion>", reasoning: "Saved reasoning", freelancerSharePercent: 0, verdict: "FavorClient", recordedAt: record.openedAt, isDemo: true } } }, "written");
  for (const text of ["Original &lt;script&gt;statement&lt;/script&gt;", "No response recorded", "Saved &lt;opinion&gt;", "Demo opinion (Mock provider)", "0%", "An AI opinion does not settle the contract or transfer funds"]) assert.ok(html.includes(text), text);
  assert.doesNotMatch(html, /<script>|Recorded settlement outcome|Arbiter review from/);
});
test("recorded settlement uses exact gross fee net and unknown refund markers", () => {
  const html = selected({ data: { ...detail, outcome: { grossAmountExact: "0.000009", platformFeePaidExact: "0", freelancerNetPaidExact: "0.000009", returnedToClientExact: null, escrowEnded: true } } });
  for (const text of ["0.000009", "Unknown / not recorded", "Recorded settlement outcome", "Payment transaction hash", "Not recorded", "Yes"]) assert.ok(html.includes(text), text);
});
test("missing or corrupt historical opinion data remains explicit and leaves financial data visible", () => {
  const html = selected({ data: { ...detail, opinion: { readable: false, summary: null, reasoning: null, freelancerSharePercent: null, verdict: null, recordedAt: null, isDemo: null } } });
  assert.match(html, /saved AI text could not be read/); assert.match(html, /Financial records are unchanged/); assert.match(html, /Original AI provider is not recorded/);
  assert.match(html, /immutable-arbiter/);
});
test("loading empty stale error and wrong-record states do not fabricate evidence", () => {
  assert.match(list({ isPending: true }), /Loading disputes/); assert.match(list({ data: page([]) }), /No saved records/);
  assert.match(selected({ isPending: true }), /Loading dispute details/);
  assert.match(selected({ data: detail, isError: true, error: new Error("Offline") }), /last successful refresh/);
  assert.doesNotMatch(selected({ data: { ...detail, record: { ...record, id: "other-case" }, reviewNote: "Unrelated secret" } }), /Unrelated secret|Saved &lt;b&gt;note/);
});
