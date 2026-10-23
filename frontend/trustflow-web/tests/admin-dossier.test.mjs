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
    if (specifier === "@/features/admin/hooks/use-admin-disputes") return { url: moduleUrl('export function useAdminDisputes(){return {};} export function useAdminDispute(){return {};}'), shortCircuit: true };
    if (specifier === "@/features/admin/hooks/use-admin-timeline") return { url: moduleUrl('export function useAdminTimeline(){return {};}'), shortCircuit: true };
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
const { AdminProjectDossierScreen } = await import("../src/features/admin/components/admin-project-dossier-screen.tsx");
const { safeDeliveryUrl } = await import("../src/features/admin/lib/safe-delivery-url.ts");
const project = { id: "11111111-1111-1111-1111-111111111111", title: "Private <project>", description: "Private <description>", status: "InProgress", clientId: null, clientName: "Former client", budgetExact: "9007199254740991.01", deadline: "2026-11-01T12:00:00Z", createdAt: "2026-10-19T12:00:00Z", participantCount: 3, milestoneCount: 4, proposalCount: 5, escrowCount: 2 };
const base = { isPending: false, isFetching: false, isError: false, refetch(){} };
const page = (items) => ({ items, totalItems: items.length, page: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false });
function render(projectState = { data: project }, recordState = { data: page([]) }) {
  globalThis.dossierProject = { ...base, ...projectState };
  globalThis.dossierRecords = { ...base, ...recordState };
  return renderToStaticMarkup(React.createElement(AdminProjectDossierScreen, { projectId: project.id }));
}
test("loading does not invent project data or records", () => {
  const html = render({ isPending: true }, { isPending: true });
  assert.match(html, /Loading project dossier/);
  assert.doesNotMatch(html, /Private|matching records/);
});
test("project metadata preserves exact budgets, escaping and read-only navigation", () => {
  const html = render();
  for (const text of ["9,007,199,254,740,991.01", "Private &lt;project&gt;", "Private &lt;description&gt;", "Former client", "No client account saved", "4 milestones", "5 proposals", "2 escrows", "Current account wallets can differ"])
    assert.ok(html.includes(text), text);
  assert.match(html, /href="\/dashboard\/admin\/projects"/);
  assert.doesNotMatch(html, /Release payment|Cancel escrow|Delete project|Send message/);
});
test("participant history and current verification are visible without inventing an assignment", () => {
  const html = render(undefined, { data: page([{ kind: "participant", id: "past", fullName: "Past freelancer", hasEscrow: true, hasProposal: true, isClient: false, currentFreelancer: false, walletAddress: null, walletVerified: false }]) });
  assert.match(html, /Past escrow participant/); assert.match(html, /Proposal author/);
  assert.match(html, /No wallet saved/); assert.match(html, /Not verified/);
  assert.doesNotMatch(html, /Current freelancer/);
});
test("milestone cards preserve tiny amounts and unassigned stages", () => {
  const html = render(undefined, { data: page([{ kind: "milestone", id: "stage", title: "Small stage", description: "<script>bad</script>", sequenceNumber: 1, status: "Pending", amountExact: "0.000001", deadline: project.deadline, freelancerName: null, freelancerId: null }]) });
  assert.match(html, /0.000001/); assert.match(html, /Unassigned/);
  assert.match(html, /&lt;script&gt;bad&lt;\/script&gt;/); assert.doesNotMatch(html, /<script>/);
});
test("proposal cards show historical status, exact offer and requested stage count", () => {
  const html = render(undefined, { data: page([{ kind: "proposal", id: "proposal", freelancerId: "person", freelancerName: "Applicant", status: "Withdrawn", coverLetter: "Prior offer", bidAmountExact: "9007199254740991.01", estimatedDays: 4, milestoneCount: 2, createdAt: project.createdAt }]) });
  assert.match(html, /Withdrawn/); assert.match(html, /9,007,199,254,740,991.01/);
  assert.match(html, /Requested milestones/); assert.match(html, /Prior offer/);
});
test("escrow cards distinguish an undeployed contract and zero-fee legacy terms", () => {
  const html = render(undefined, { data: page([{ kind: "escrow", id: "escrow", freelancerId: "past", freelancerName: "Historical freelancer", status: "PendingDeployment", chainId: 31337, tokenAddress: "token", contractAddress: null, clientWalletAddress: null, freelancerWalletAddress: "snapshot-wallet", arbiterWalletAddress: null, feeRecipientAddress: null, platformFeeBasisPoints: 0, tokenDecimals: null, totalAmountExact: "9007199254740991.01", releasedAmountExact: "0.000001", createdAt: project.createdAt, updatedAt: project.createdAt }]) });
  for (const text of ["Pending Deployment", "Not deployed", "Not recorded", "snapshot-wallet", "0%", "0.000001", "31337"]) assert.ok(html.includes(text), text);
  assert.doesNotMatch(html, /Connect wallet|Deploy contract|Release payment/);
});
test("missing, stale and section errors retain recovery and honest content", () => {
  const missing = render({ isError: true, error: { isAxiosError: true, response: { status: 404 } } });
  assert.match(missing, /Project not found/); assert.doesNotMatch(missing, /Private/);
  const stale = render({ data: project, isError: true, error: new Error("Offline") }, { isError: true, error: new Error("Offline"), data: page([]) });
  assert.match(stale, /last successful refresh/); assert.match(stale, /Retry records/);
  assert.match(stale, /No records on this page/); assert.match(stale, /Page 1 of 1/);
});

const delivery = { kind: "delivery", id: "submission", milestoneId: "milestone", milestoneTitle: "Homepage", sequenceNumber: 1,
  currentMilestoneStatus: "Approved", freelancerId: "past-author", freelancerName: "Original freelancer", version: 1, isLatest: false,
  note: "Delivered <script>unsafe</script>", previewLink: "https://example.com/preview", sourceFilesLink: "javascript:alert(1)",
  clientFeedback: "Please change <b>the title</b>", submittedAt: project.createdAt, reviewedAt: project.createdAt,
  attachments: [{ id: "file", fileName: "design <draft>.pdf", contentType: "application/pdf", fileSize: 1234, sha256: "abc123" }] };
test("delivery history keeps the original author, version, feedback and file metadata", () => {
  const html = render(undefined, { data: page([delivery]) });
  for (const text of ["Version 1", "Earlier version", "Original freelancer", "past-author", "Client requested changes to this version", "Please change &lt;b&gt;the title&lt;/b&gt;", "design &lt;draft&gt;.pdf", "abc123", "application/pdf", "Current milestone status"])
    assert.ok(html.includes(text), text);
  assert.match(html, /Delivered &lt;script&gt;unsafe&lt;\/script&gt;/);
  assert.doesNotMatch(html, /<script>|href="javascript:|Approve delivery|Release payment|Request changes<\/button>/);
});
test("delivery links are manual safe external links and never embedded previews", () => {
  const html = render(undefined, { data: page([delivery]) });
  assert.match(html, /href="https:\/\/example.com\/preview"/);
  assert.match(html, /rel="noopener noreferrer"/);
  assert.doesNotMatch(html, /<iframe|<embed|<object|javascript:alert/);
});
test("no feedback on the latest version does not invent client approval", () => {
  const html = render(undefined, { data: page([{ ...delivery, version: 2, isLatest: true, clientFeedback: null, reviewedAt: null, attachments: [] }]) });
  assert.match(html, /Latest version for this milestone/);
  assert.match(html, /does not confirm approval or payment/);
  assert.match(html, /No files attached to this version/);
  assert.doesNotMatch(html, /Client requested changes to this version/);
});
test("unsafe or credential-bearing delivery URLs are refused", () => {
  for (const value of [null, "relative/path", "javascript:alert(1)", "data:text/html,bad", "file:///tmp/x", "https://user:secret@example.com/x"])
    assert.equal(safeDeliveryUrl(value), null);
  assert.equal(safeDeliveryUrl("https://example.com/a"), "https://example.com/a");
  assert.equal(safeDeliveryUrl("http://localhost:3000/preview"), "http://localhost:3000/preview");
});
