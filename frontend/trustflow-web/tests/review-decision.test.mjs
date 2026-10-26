import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { registerHooks } from "node:module";
import { test } from "node:test";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
const code = (source) => ({ url: `data:text/javascript,${encodeURIComponent(source)}`, shortCircuit: true });
registerHooks({
  resolve(specifier, context, nextResolve) {
    if (specifier === "@/features/review/hooks/use-review-cases") return code('export function useReviewCase(){return {data:globalThis.reviewDetail};} export function useReviewCases(){return {data:globalThis.reviewRows};} export function useSaveReviewNote(){return {};} export function useSyncReviewCase(){return {};} export function useAdviseReviewCase(){return {};}');
    if (specifier === "@/features/review/hooks/use-settle-case") return code('export function useSettleCase(){return {};}');
    if (specifier === "@/features/review/hooks/use-review-case-readiness") return code('export function useReviewCaseReadiness(){return {status:globalThis.chainReadiness ?? "ready",ready:(globalThis.chainReadiness ?? "ready")==="ready"};}');
    if (specifier === "@/features/review/api/review-api") return code('export function downloadReviewFile(){throw new Error("No downloads in render");}');
    if (specifier === "@/stores/auth-store") return code('export function useAuthStore(selector){return selector({readOnly:globalThis.preview});}');
    if (specifier === "wagmi") return code('export function useConnection(){return {address:globalThis.connectedWallet};}');
    if (specifier === "next/link") return nextResolve("next/link.js", context);
    if (specifier.startsWith("@/")) {
      const base = new URL(`../src/${specifier.slice(2)}`, import.meta.url);
      for (const ext of [".ts", ".tsx"]) if (existsSync(fileURLToPath(base.href + ext))) return nextResolve(base.href + ext, context);
    }
    return nextResolve(specifier, context);
  },
  load(url, context, nextLoad) {
    if (url.endsWith(".tsx")) return { format: "module", shortCircuit: true, source: ts.transpileModule(readFileSync(new URL(url), "utf8"), { compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText };
    return nextLoad(url, context);
  },
});
const { ReviewCaseScreen } = await import("../src/features/review/components/review-case-screen.tsx");
const { ReviewDeskScreen } = await import("../src/features/review/components/review-desk-screen.tsx");
const detail = { case: { id: "case", referenceCode: "TF-SHARED-REFERENCE", milestoneTitle: "Delivery", projectTitle: "Example project", milestoneAmount: 100, milestoneAmountExact: "100", kind: "PaymentRequest", status: "Open", openedAt: "2026-10-01T12:00:00Z", settleableAt: "2026-10-15T12:00:00Z", isReady: true }, projectDescription: "Brief", milestoneDescription: "Agreed work", milestoneSequence: 1, milestoneDeadline: "2026-11-01T00:00:00Z", deadlinePassed: false, milestoneStatus: "Submitted", tokenDecimals: 6, platformFeeBasisPoints: 1000, arbiterWalletAddress: "arbiter-wallet", deliveries: [], advice: null, dispute: null, aiEnabled: false, reviewNote: "A short shared decision reason." };
function render(data = detail, preview = false, wallet = "arbiter-wallet") {
 globalThis.reviewDetail = data; globalThis.preview = preview; globalThis.connectedWallet = wallet;
 return renderToStaticMarkup(React.createElement(ReviewCaseScreen, { caseId: "case" }));
}
test("reviewer must choose a decision explicitly and sees all four split paths", () => {
 const html = render();
 for (const text of ["Refund client in full", "Award freelancer in full", "Percentage split", "Exact amount", "TF-SHARED-REFERENCE", "Decide only this disputed milestone", "Gross freelancer award", "Platform fee", "Freelancer receives", "both sides will read this"])
   assert.ok(html.includes(text), text);
 assert.match(html, /<button[^>]*disabled=""[^>]*>Sign the settlement/);
 assert.doesNotMatch(html, /round down to whole cents|Nothing<\/button>|Half<\/button>/);
});
test("local preview cannot edit or sign a financial decision", () => {
 const html = render(detail, true);
 assert.match(html, /Local preview is read-only/);
 assert.match(html, /<button[^>]*disabled=""[^>]*>Percentage split/);
 assert.match(html, /<textarea[^>]*disabled=""/);
 assert.match(html.match(/<button[^>]*>[\s\S]*?<\/button>/g).find((button) => button.includes("Read the contract again")), /disabled=""/);
});
test("closed result keeps the shared reference, exact net and real transaction proof", () => {
 const html = render({ ...detail, reviewNote: "Saved <script>reason</script>", case: { ...detail.case, status: "Settled", freelancerNetPaid: 0.000009, freelancerNetPaidExact: "0.000009", paidToFreelancer: 0.00001, platformFeePaid: 0.000001, returnedToClient: 99.99999, returnedToClientExact: "99.99999", settlementTransactionHash: "verified-settlement-proof", settlementAt: "2026-10-20T12:00:00Z" } });
 for (const text of ["TF-SHARED-REFERENCE", "verified-settlement-proof", "0.000009", "99.99999", "Saved &lt;script&gt;reason&lt;/script&gt;"]) assert.ok(html.includes(text), text);
 assert.doesNotMatch(html, /Sign the settlement|<textarea|<script>/);
});
test("case detail uses chain readiness rather than server wall-clock eligibility", () => {
 globalThis.chainReadiness = "ready";
 const ready = render({ ...detail, case: { ...detail.case, isReady: false } });
 assert.match(ready, /The chain confirms that the 14-day response window has ended/);
 assert.doesNotMatch(ready, /The 14 days are over, so you can decide/);
 globalThis.chainReadiness = "waiting";
 const waiting = render({ ...detail, case: { ...detail.case, isReady: true } });
 assert.match(waiting, /The 14-day response window has not ended on the chain/);
 assert.doesNotMatch(waiting, /The chain confirms that the 14-day/);
 globalThis.chainReadiness = "unavailable";
 assert.match(render(), /Current chain eligibility could not be confirmed/);
 globalThis.chainReadiness = "ready";
});
test("the review list labels wall-clock readiness as an estimate requiring a chain check", () => {
 globalThis.reviewRows = [detail.case];
 const html = renderToStaticMarkup(React.createElement(ReviewDeskScreen));
 assert.match(html, /Estimated window ended · open to check chain/);
 assert.doesNotMatch(html, /Ready to decide/);
});
