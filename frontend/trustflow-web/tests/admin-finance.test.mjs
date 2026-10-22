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
    if (specifier === "@/features/admin/hooks/use-admin-finance") return { url: moduleUrl('export function useAdminFinance(){return globalThis.financeOverview;} export function useAdminFinanceEntries(){return globalThis.financeEntries;}'), shortCircuit: true };
    if (specifier === "@/features/admin/api/admin-finance-api") return { url: moduleUrl('export async function downloadAdminFinanceReport(){throw new Error("No network during render");}'), shortCircuit: true };
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
const { AdminFinancialHistory } = await import("../src/features/admin/components/admin-financial-history.tsx");
const base = { isPending: false, isFetching: false, isError: false, refetch(){} };
const page = (items) => ({ items, totalItems: items.length, page: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false });
const escrow = { id: "escrow-one", freelancerId: "talent", freelancerName: "Reza <talent>", status: "Completed", chainId: 31337,
  tokenAddress: "token-on-local-chain", contractAddress: "local-contract", tokenDecimals: 6, totalAmountExact: "100", fundingAmountExact: "100",
  grossReleasedExact: "100", platformFeesPaidExact: "10", freelancerNetPaidExact: "90", recordedCaseRefundsExact: "0", cancellationRefundExact: null,
  remainingRecordedAmountExact: "0", gaps: [], deploymentTransactionHash: "deployment-proof", fundingTransactionHash: "funding-proof",
  cancellationTransactionHash: null, fundedAt: "2026-10-19T12:00:00Z", cancelledAt: null, updatedAt: "2026-10-19T12:00:00Z" };
const payout = { kind: "payout", id: "payout-one", title: "Release <one>", status: "Approved", grossAmountExact: "100",
  platformFeePaidExact: "10", freelancerNetPaidExact: "90", caseId: null, recordedAt: null };
function render(overview = { data: page([escrow]) }, entries = { data: page([payout]) }) {
  globalThis.financeOverview = { ...base, ...overview }; globalThis.financeEntries = { ...base, ...entries };
  return renderToStaticMarkup(React.createElement(AdminFinancialHistory, { projectId: "project-one" }));
}
test("financial history displays saved gross, fee, net and proof without wallet controls", () => {
  const html = render();
  for (const text of ["Financial history", "Reza &lt;talent&gt;", "Gross released", "Platform fees paid", "Net paid to freelancer", "deployment-proof", "funding-proof", "not live wallet balances", "already included", "Payment transaction hash", "Not recorded"])
    assert.ok(html.includes(text), text);
  assert.match(html, /Full cancellation refund<\/dt><dd[^>]*>Not applicable<\/dd>/);
  assert.doesNotMatch(html, /Connect wallet|Cancel escrow|Approve delivery|Release payment|<script>/);
});
test("large and tiny exact strings survive without floating point conversion", () => {
  const html = render({ data: page([{ ...escrow, totalAmountExact: "9007199254740991.01", platformFeesPaidExact: "0.000001", freelancerNetPaidExact: "0.000009" }]) },
    { data: page([{ ...payout, grossAmountExact: "0.000009", platformFeePaidExact: "0", freelancerNetPaidExact: "0.000009" }]) });
  for (const value of ["9,007,199,254,740,991.01", "0.000001", "0.000009"]) assert.ok(html.includes(value), value);
});
test("unknown amounts remain visibly distinct from confirmed zero", () => {
  const html = render({ data: page([{ ...escrow, status: "Cancelled", fundingAmountExact: null, cancellationRefundExact: "0", remainingRecordedAmountExact: null,
    gaps: ["Funding evidence is not recorded."] }]) }, { data: page([{ ...payout, grossAmountExact: null, platformFeePaidExact: null, freelancerNetPaidExact: null }]) });
  assert.match(html, /Unknown \/ not recorded/); assert.match(html, /Funding evidence is not recorded/);
  assert.match(html, /Full cancellation refund<\/dt><dd[^>]*>0<\/dd>/);
  assert.doesNotMatch(html, /Recorded settlement time<\/dt><dd[^>]*>2026/);
});
test("contracts on different networks stay separate with their own token identity", () => {
  const html = render({ data: page([escrow, { ...escrow, id: "escrow-two", chainId: 1, tokenAddress: "other-token", freelancerName: "Other talent" }]) });
  for (const value of ["31337", "other-token", "escrow-two", "token-on-local-chain", "not combined across contracts"]) assert.ok(html.includes(value), value);
  assert.doesNotMatch(html, /Grand total|All-contract total/);
});
test("settled-case refunds display an outcome without creating another payout", () => {
  const html = render(undefined, { data: page([{ kind: "settlement", id: "case-one", milestoneId: "stage-one", milestoneTitle: "Settled stage",
    grossAmountExact: "20", platformFeePaidExact: "2", freelancerNetPaidExact: "18", returnedToClientExact: "30", escrowEnded: true, closedAt: null }]) });
  for (const value of ["Settled stage", "Returned to client", "Escrow ended", "Yes", "stage-one", "Recorded closure time"]) assert.ok(html.includes(value), value);
  assert.doesNotMatch(html, /Settle case<\/button>|Sign settlement/);
});
test("loading, empty and stale failures provide recovery without fabricated records", () => {
  const loading = render({ isPending: true }, {}); assert.match(loading, /Loading financial records/); assert.doesNotMatch(loading, /Reza/);
  const empty = render({ data: page([]) }, {}); assert.match(empty, /No escrow financial records/); assert.doesNotMatch(empty, /Selected escrow financial entries/);
  const stale = render({ data: page([escrow]), isError: true, error: new Error("Offline") }, { data: page([]), isError: true });
  assert.match(stale, /last successful refresh/); assert.match(stale, /Retry entries/); assert.match(stale, /No recorded entries/);
});

test("private export describes its full selected-escrow scope and exact-text import", () => {
  const html = render();
  for (const text of ["Confidential project financial records", "report downloads are recorded", "only with authorized people", "Download selected escrow report (CSV)", "not just this page", "value column as Text"])
    assert.ok(html.includes(text), text);
  assert.doesNotMatch(render({ data: page([]) }, {}), /Download selected escrow report/);
});
