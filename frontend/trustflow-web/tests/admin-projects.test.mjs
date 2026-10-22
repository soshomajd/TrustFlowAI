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
const queryMock = 'export function useAdminProjects(){return globalThis.adminProjectsTestQuery;}';
const authMock = 'export function useAuthStore(select){return select({user:{id:"test-admin",fullName:"Test Admin",roles:["Admin"]},status:"authenticated"});}';
const moduleUrl = (source) => `data:text/javascript,${encodeURIComponent(source)}`;

registerHooks({
  resolve(specifier, context, nextResolve) {
    if (specifier === "next/link") return nextResolve("next/link.js", context);
    if (specifier === "@/features/admin/hooks/use-admin-projects") return { url: moduleUrl(queryMock), shortCircuit: true };
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

const { AdminProjectsScreen } = await import("../src/features/admin/components/admin-projects-screen.tsx");
const { emptyProjectFilters, buildAdminProjectQuery } = await import("../src/features/admin/lib/project-filters.ts");
const { getDashboardNavigation } = await import("../src/config/dashboard-navigation.ts");

function render(state) {
  globalThis.adminProjectsTestQuery = { isPending: false, isFetching: false, isError: false, refetch() {}, ...state };
  return renderToStaticMarkup(React.createElement(AdminProjectsScreen));
}
const project = {
  id: "11111111-1111-1111-1111-111111111111", title: "Private <project>", status: "Open",
  clientId: "22222222-2222-2222-2222-222222222222", clientName: "Sara Client", budgetExact: "9007199254740991.01",
  createdAt: "2026-10-18T12:00:00Z", milestoneCount: 2, escrowCount: 1, proposalCount: 3, openCaseCount: 1,
  freelancers: [{ id: "33333333-3333-3333-3333-333333333333", fullName: "Reza Freelancer" }],
};
const page = { items: [project], page: 1, pageSize: 20, totalItems: 21, totalPages: 2, hasPreviousPage: false, hasNextPage: true };

test("the project list is visible in admin navigation only", () => {
  for (const roles of [["Client"], ["Freelancer"], []])
    assert.equal(getDashboardNavigation(roles).some(item => item.href === "/dashboard/admin/projects"), false);
  assert.equal(getDashboardNavigation(["Admin"]).some(item => item.href === "/dashboard/admin/projects"), true);
});

test("new filters always reset paging and trim search/account IDs", () => {
  const query = buildAdminProjectQuery({ ...emptyProjectFilters, search: "  Sara  ", status: "Completed", pageSize: "50", clientId: ` ${project.clientId} `, freelancerId: project.freelancers[0].id });
  assert.equal(query.page, 1);
  assert.equal(query.pageSize, 50);
  assert.equal(query.search, "Sara");
  assert.equal(query.status, "Completed");
  assert.equal(query.clientId, project.clientId);
  assert.equal(query.freelancerId, project.freelancers[0].id);
  assert.equal(query.from, undefined);
});

test("UTC date filters include the whole last day and handle leap years", () => {
  const query = buildAdminProjectQuery({ ...emptyProjectFilters, from: "2028-02-28", through: "2028-02-29" });
  assert.equal(query.from, "2028-02-28T00:00:00.000Z");
  assert.equal(query.to, "2028-03-01T00:00:00.000Z");
  assert.equal(buildAdminProjectQuery({ ...emptyProjectFilters, through: "2026-12-31" }).to, "2027-01-01T00:00:00.000Z");
});

test("bad search, status, IDs, page sizes and reversed/invalid dates are refused", () => {
  for (const fields of [{ search: "ab" }, { search: "x".repeat(201) }, { status: "NotAStatus" }, { clientId: "bad" }, { freelancerId: "bad" }, { pageSize: "5000" }, { from: "2026-02-30" }, { through: "2026-02-29" }, { through: "9999-12-31" }, { from: "2026-11-01", through: "2026-10-01" }])
    assert.throws(() => buildAdminProjectQuery({ ...emptyProjectFilters, ...fields }));
});

test("the loading list does not invent rows or counts", () => {
  const html = render({ isPending: true, isFetching: true });
  assert.match(html, /Loading projects/);
  assert.doesNotMatch(html, /Admin project records/);
  assert.doesNotMatch(html, /matching projects/);
});

test("rendered records preserve exact money and escape titles while showing parties and counts", () => {
  const html = render({ data: page });
  for (const text of ["9,007,199,254,740,991.01", "Sara Client", "Reza Freelancer", "2 stages", "1 escrows", "3 proposals", "1 open cases", "21 matching projects", "Page 1 of 2"])
    assert.ok(html.includes(text), text);
  assert.match(html, /Private &lt;project&gt;/);
  assert.match(html, /Access to this list and each displayed project is recorded/);
  assert.ok(html.includes(`href="/dashboard/admin/projects/${project.id}"`));
  assert.doesNotMatch(html, /Release payment|Cancel escrow|Delete project/);
});

test("an empty page keeps filters and honest navigation", () => {
  const html = render({ data: { ...page, items: [], totalItems: 0, totalPages: 0, hasNextPage: false } });
  assert.match(html, /No projects on this page/);
  assert.match(html, /0 matching projects/);
  assert.match(html, /Page 1 of 1/);
  assert.doesNotMatch(html, /Admin project records/);
});

test("error and stale refresh states are explicit with retry available", () => {
  const failed = render({ isError: true, error: new Error("Unavailable") });
  assert.match(failed, /Could not load projects/);
  assert.match(failed, /Refresh projects/);
  assert.doesNotMatch(failed, /Admin project records/);
  const stale = render({ data: page, isError: true, error: new Error("Unavailable") });
  assert.match(stale, /last successful refresh/);
  assert.match(stale, /Sara Client/);
});
