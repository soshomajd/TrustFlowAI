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
    if (specifier === "next/image") return nextResolve("next/image.js", context);
    if (specifier === "@/features/milestones/hooks/use-milestone-submissions") {
      return { url: moduleUrl("export function useMilestoneSubmissions(){return globalThis.deliveryQueryState;}"), shortCircuit: true };
    }
    if (specifier === "@/features/milestones/api/milestones-api") {
      return { url: moduleUrl(`
        export async function downloadMilestoneSubmissionFile(){throw new Error("No download during render");}
        export async function downloadMilestoneSubmissionPreview(){throw new Error("No preview during render");}
        export const getMilestoneSubmissionPreview = downloadMilestoneSubmissionPreview;
      `), shortCircuit: true };
    }
    if (specifier.startsWith("@/")) {
      const base = new URL(`../src/${specifier.slice(2)}`, import.meta.url);
      for (const extension of [".ts", ".tsx"]) {
        if (existsSync(fileURLToPath(base.href + extension))) return nextResolve(base.href + extension, context);
      }
    }
    return nextResolve(specifier, context);
  },
  load(url, context, nextLoad) {
    if (url.endsWith(".tsx")) return { format: "module", shortCircuit: true,
      source: ts.transpileModule(readFileSync(new URL(url), "utf8"), {
        compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
      }).outputText };
    return nextLoad(url, context);
  },
});

const { MilestoneSubmissionsPanel } = await import("../src/features/milestones/components/milestone-submissions-panel.tsx");
const { invalidateEscrowWorkflow } = await import("../src/features/escrow/queries/invalidate-escrow-workflow.ts");
const sourceLink = "https://private-source.example.test/full-original";
const original = {
  id: "delivery-two", milestoneId: "milestone-one", version: 2,
  note: "Revised <design>", submittedAt: "2026-10-29T10:00:00Z",
  previewLink: "https://review.example.test/demo", sourceFilesLink: sourceLink,
  originalsUnlocked: false, hasSourceFiles: true,
  clientFeedback: null, reviewedAt: null,
  attachments: [{ id: "file-one", fileName: "Artwork.png", contentType: "image/png", fileSize: 12000, sha256: "file-hash", hasPreview: true }],
};
function render(items = [original], role = "Client", state = {}) {
  globalThis.deliveryQueryState = { data: items, isLoading: false, isError: false, isFetching: false, refetch() {}, ...state };
  return renderToStaticMarkup(React.createElement(MilestoneSubmissionsPanel, {
    projectId: "project-one", milestoneId: "milestone-one", viewerRole: role,
  }));
}

test("unpaid Client sees a review preview but no original or private source URL", () => {
  const html = render();
  assert.match(html, /Review preview/);
  assert.match(html, /Artwork\.png/);
  assert.match(html, /https:\/\/review\.example\.test\/demo/);
  assert.doesNotMatch(html, /Download original|Open source files|private-source\.example/);
});

test("a missing entitlement fails closed even if an older cache includes a source URL", () => {
  const legacy = { ...original };
  delete legacy.originalsUnlocked;
  const html = render([legacy]);
  assert.match(html, /Review preview/);
  assert.doesNotMatch(html, /Download original|Open source files|private-source\.example/);
});

test("confirmed full-payment entitlement exposes the original and source files", () => {
  const html = render([{ ...original, originalsUnlocked: true }]);
  assert.match(html, /Download original/);
  assert.match(html, /Open source files/);
  assert.match(html, /https:\/\/private-source\.example\.test\/full-original/);
});

test("Freelancer can still retrieve their own originals before Client payment", () => {
  const html = render([original], "Freelancer");
  assert.match(html, /Download original/);
  assert.match(html, /Open source files/);
});

test("every historical version respects the same Client gate and retains feedback", () => {
  const old = { ...original, id: "delivery-one", version: 1,
    clientFeedback: "Please change <the title>", reviewedAt: "2026-10-29T09:00:00Z" };
  const html = render([original, old]);
  assert.match(html, /Previous versions \(1\)/);
  assert.match(html, /Version 1/);
  assert.match(html, /Version 2/);
  assert.match(html, /Please change &lt;the title&gt;/);
  assert.match(html, /Revised &lt;design&gt;/);
  assert.doesNotMatch(html, /Download original|private-source\.example/);
});

test("source-only deliveries remain visibly locked without leaking their URL", () => {
  const html = render([{ ...original, attachments: [], previewLink: null }]);
  assert.match(html, /[Ll]ocked|full payment/i);
  assert.doesNotMatch(html, /Open source files|private-source\.example/);
});

test("missing preview support never falls back to downloading the original", () => {
  const html = render([{ ...original, attachments: [{ ...original.attachments[0], hasPreview: false }] }]);
  assert.doesNotMatch(html, /Review preview|Download original/);
  assert.match(html, /Artwork\.png/);
});

test("query errors offer retry without inventing paid access", () => {
  const html = render([], "Client", { isError: true, error: new Error("Offline") });
  assert.match(html, /Retry/);
  assert.doesNotMatch(html, /Download original|Open source files/);
});

test("reconciliation invalidates delivery entitlements as well as project state", async () => {
  const keys = [];
  await invalidateEscrowWorkflow({ invalidateQueries: async ({ queryKey }) => keys.push(queryKey) }, "project-one");
  assert.ok(keys.some(key => key.length === 1 && key[0] === "milestones"));
  assert.ok(keys.some(key => key[0] === "escrows" || key[0] === "escrow"));
});
