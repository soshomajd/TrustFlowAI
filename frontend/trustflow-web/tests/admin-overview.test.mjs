import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { registerHooks } from "node:module";
import { test } from "node:test";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";

// Render the production screen with controlled query states. API authorization
// and real persisted counts/auditing are covered in the PostgreSQL tests.
const queryMock = 'export function useAdminOverview(){return globalThis.adminOverviewTestQuery;}';
const authMock = 'export function useAuthStore(select){return select({user:{id:"test-admin",fullName:"Test Admin",roles:["Admin"]},status:"authenticated"});}';
const moduleUrl = (source) => `data:text/javascript,${encodeURIComponent(source)}`;

registerHooks({
  resolve(specifier, context, nextResolve) {
    if (specifier === "next/link") return nextResolve("next/link.js", context);
    if (specifier === "@/features/admin/hooks/use-admin-overview") return { url: moduleUrl(queryMock), shortCircuit: true };
    if (specifier === "@/stores/auth-store") return { url: moduleUrl(authMock), shortCircuit: true };
    if (specifier.startsWith("@/")) {
      const base = new URL(`../src/${specifier.slice(2)}`, import.meta.url);
      for (const extension of [".ts", ".tsx"]) {
        const url = base.href + extension;
        if (existsSync(fileURLToPath(url))) return nextResolve(url, context);
      }
    }
    return nextResolve(specifier, context);
  },
  load(url, context, nextLoad) {
    if (url.endsWith(".tsx")) return {
      format: "module", shortCircuit: true,
      source: ts.transpileModule(readFileSync(new URL(url), "utf8"), {
        compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
      }).outputText,
    };
    return nextLoad(url, context);
  },
});

const { AdminOverviewScreen } = await import("../src/features/admin/components/admin-overview-screen.tsx");
const { getDashboardHome } = await import("../src/features/auth/lib/get-dashboard-home.ts");
const { getDashboardNavigation } = await import("../src/config/dashboard-navigation.ts");

function render(state) {
  globalThis.adminOverviewTestQuery = { isPending: false, isFetching: false, isError: false, refetch() {}, ...state };
  return renderToStaticMarkup(React.createElement(AdminOverviewScreen));
}
const counts = { totalProjects: 15, openProjects: 8, inProgressProjects: 3, completedProjects: 3, cancelledProjects: 1, totalEscrows: 9, openCases: 2, generatedAt: "2026-10-18T12:00:00Z" };

test("admins reach their overview even when they also hold a participant role", () => {
  for (const roles of [["Admin"], ["Client", "Admin"], ["Freelancer", "Admin"]])
    assert.equal(getDashboardHome(roles), "/dashboard/admin");
  assert.equal(getDashboardHome(["Client"]), "/dashboard/client");
  assert.equal(getDashboardHome(["Freelancer"]), "/dashboard/freelancer");
  assert.equal(getDashboardHome([]), "/");
});

test("ordinary roles do not receive admin overview or reviewer navigation", () => {
  for (const roles of [["Client"], ["Freelancer"], []])
    assert.equal(getDashboardNavigation(roles).some(item => ["/dashboard/admin", "/dashboard/review"].includes(item.href)), false);
  assert.equal(getDashboardNavigation(["Admin"]).some(item => item.href === "/dashboard/admin"), true);
});

test("a pending overview does not invent zero statistics", () => {
  const html = render({ isPending: true, isFetching: true });
  assert.match(html, /Loading platform overview/);
  assert.doesNotMatch(html, /Platform statistics/);
});

test("API failures show an error and a retry control, not fake counts", () => {
  const html = render({ isError: true, error: new Error("Unavailable") });
  assert.match(html, /Could not load the overview/);
  assert.match(html, /Refresh overview/);
  assert.doesNotMatch(html, /Platform statistics/);
});

test("the overview renders all status categories, escrow and case counts", () => {
  const html = render({ data: counts });
  for (const label of ["All projects", "Open", "In progress", "Completed", "Cancelled", "Escrow records", "Open cases"])
    assert.ok(html.includes(label));
  assert.match(html, />15<\/p>/);
  assert.match(html, /Admin access is recorded/);
  assert.match(html, /href="\/dashboard\/review"/);
  assert.doesNotMatch(html, /No projects have been created/);
});

test("a successful empty response has an honest empty state", () => {
  const empty = Object.fromEntries(Object.keys(counts).map(key => [key, key === "generatedAt" ? counts.generatedAt : 0]));
  const html = render({ data: empty });
  assert.match(html, /No projects have been created yet/);
  assert.match(html, /Platform statistics/);
});

test("a failed refresh labels previously successful counts as old", () => {
  const html = render({ data: counts, isError: true, error: new Error("Unavailable") });
  assert.match(html, /last successful refresh/);
  assert.match(html, />15<\/p>/);
});
