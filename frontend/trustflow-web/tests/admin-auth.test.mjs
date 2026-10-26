import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { registerHooks } from "node:module";
import { test } from "node:test";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

// Render the production screen with controlled query states. API authorization
// and real persisted counts/auditing are covered in the PostgreSQL tests.
const moduleUrl = (source) => `data:text/javascript,${encodeURIComponent(source)}`;
const authMock = 'export function useAuthStore(select){return select(globalThis.adminAuthTestState || {user:null,status:"unauthenticated"});}';
const adminMock = 'export function useAdminLogin(){return {isPending:false,mutate(){}};} export const useAdminSetup=useAdminLogin; export const useAdminMfa=useAdminLogin;';
const navigationMock = 'export function useRouter(){return {replace(){},refresh(){},push(){}};} export function usePathname(){return "/dashboard/admin";} export function notFound(){throw new Error("Route not found");}';
const recoveryMock = 'const hook=()=>({isPending:false,mutate(){}}); export const useVerifyEmail=hook,useResendVerification=hook,useForgotPassword=hook,useResetPassword=hook,useResendResetCode=hook; export function getResendWaitSeconds(){return null;}';

registerHooks({
  resolve(specifier, context, nextResolve) {
    if (specifier === "next/link") return nextResolve("next/link.js", context);
    if (specifier === "@/features/admin/hooks/use-admin-auth") return { url: moduleUrl(adminMock), shortCircuit: true };
    if (specifier === "@/features/admin/api/admin-auth-api") return { url: moduleUrl('export async function enterLocalAdminPreview(){throw new Error("No network during render");}'), shortCircuit: true };
    if (specifier === "next/navigation") return { url: moduleUrl(navigationMock), shortCircuit: true };
    if (specifier === "@/features/auth/hooks/use-account-recovery") return { url: moduleUrl(recoveryMock), shortCircuit: true };
    if (specifier === "@/features/auth/services/restore-session") return { url: moduleUrl('export async function restoreSession(){}'), shortCircuit: true };
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

const { AdminLoginForm } = await import("../src/features/admin/components/admin-login-form.tsx");
const { AdminMfaForm } = await import("../src/features/admin/components/admin-mfa-form.tsx");
const { LocalAdminPreview } = await import("../src/features/admin/components/local-admin-preview.tsx");
const { LocalAdminSessionNotice } = await import("../src/features/admin/components/local-admin-session-notice.tsx");
const { default: LocalAdminPage } = await import("../src/app/(auth)/admin/local/page.tsx");
const { AdminSetupForm } = await import("../src/features/admin/components/admin-setup-form.tsx");
const { VerifyEmailForm } = await import("../src/features/auth/components/verify-email-form.tsx");
const { ForgotPasswordForm } = await import("../src/features/auth/components/forgot-password-form.tsx");
const { ResetPasswordForm } = await import("../src/features/auth/components/reset-password-form.tsx");
const { ProtectedRoute } = await import("../src/features/auth/components/protected-route.tsx");
const { getSignInPath, getAdminReturnPath } = await import("../src/features/auth/lib/sign-in-path.ts");
const { adminSetupSchema } = await import("../src/features/admin/schemas/admin-setup-schema.ts");
const render = (component, props = {}) => renderToStaticMarkup(React.createElement(component, props));

test("local administrator preview has no password, email-code or wallet input", () => {
  const html = renderToStaticMarkup(React.createElement(QueryClientProvider, { client: new QueryClient() }, React.createElement(LocalAdminPreview)));
  assert.match(html, /Opening the administrator panel/);
  assert.doesNotMatch(html, /<input|type="password"|Connect wallet|Verification code/);
});

test("local session notice follows the issued capability rather than a server flag", () => {
  globalThis.adminAuthTestState = { readOnly: true, localReview: false };
  assert.match(render(LocalAdminSessionNotice), /Local preview · read-only access/);
  globalThis.adminAuthTestState = { readOnly: false, localReview: true };
  const local = render(LocalAdminSessionNotice);
  assert.match(local, /Local development review · decisions enabled/);
  assert.match(local, /arbiter wallet signature required/);
  assert.doesNotMatch(local, /read-only|no wallet required/);
  globalThis.adminAuthTestState = { readOnly: false, localReview: false };
  assert.equal(render(LocalAdminSessionNotice), "");
  globalThis.adminAuthTestState = undefined;
});

test("direct local administrator entry stays unavailable outside explicitly enabled development", () => {
  const previousEnvironment = process.env.NODE_ENV;
  const previousFlag = process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW;
  try {
    process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW = "1";
    for (const environment of ["production", "test"]) {
      process.env.NODE_ENV = environment;
      assert.throws(() => LocalAdminPage(), /Route not found/);
    }
    process.env.NODE_ENV = "development";
    process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW = "0";
    assert.throws(() => LocalAdminPage(), /Route not found/);
    process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW = "1";
    const html = renderToStaticMarkup(React.createElement(QueryClientProvider,
      { client: new QueryClient() }, React.createElement(LocalAdminPage)));
    assert.match(html, /Opening the administrator panel/);
    assert.doesNotMatch(html, /<input|type="password"|Verification code/);
  } finally {
    if (previousEnvironment === undefined) delete process.env.NODE_ENV;
    else process.env.NODE_ENV = previousEnvironment;
    if (previousFlag === undefined) delete process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW;
    else process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW = previousFlag;
  }
});

test("anonymous admin and reviewer destinations use the dedicated sign-in route", () => {
  for (const path of ["/dashboard/admin", "/dashboard/admin/projects", "/dashboard/review", "/dashboard/review/case-id"])
    assert.equal(getSignInPath(path), `/admin/login?next=${encodeURIComponent(path)}`);
  for (const path of ["/dashboard/client", "/dashboard/freelancer/projects", "/dashboard/messages"])
    assert.equal(getSignInPath(path), `/login?next=${encodeURIComponent(path)}`);
});

test("admin return destinations refuse external and participant URLs", () => {
  for (const path of [undefined, "https://example.com", "//example.com", "/dashboard/client", "/dashboard/admin/../client", "/dashboard/admin\\evil", "/dashboard/administrator", "/dashboard/admin/%2f%2fexample.com"])
    assert.equal(getAdminReturnPath(path), "/dashboard/admin");
  assert.equal(getAdminReturnPath("/dashboard/admin/projects"), "/dashboard/admin/projects");
  assert.equal(getAdminReturnPath("/dashboard/review/case-id"), "/dashboard/review/case-id");
});

test("administrator sign-in has separate wording and no public signup or role picker", () => {
  const html = render(AdminLoginForm);
  assert.match(html, /Administrator sign in/);
  assert.match(html, /TrustFlow administration/);
  assert.match(html, /href="\/admin\/setup"/);
  assert.match(html, /href="\/admin\/forgot-password"/);
  assert.doesNotMatch(html, /Welcome back|Create account|href="\/register"|proposals and milestones/);
});

test("approved activation asks for the owner's password without a participant-role selector", () => {
  const html = render(AdminSetupForm);
  assert.match(html, /Activate administrator access/);
  assert.match(html, /Only an email approved by the platform owner/);
  assert.match(html, /Choose a password/);
  assert.match(html, /Confirm password/);
  assert.doesNotMatch(html, /<select|Freelancer|Client|href="\/register"/);
  assert.match(html, /href="\/admin\/login"/);
});

test("activation enforces password confirmation and never accepts caller-selected roles", () => {
  const values = { fullName: "Administrator", email: "admin@trustflow.test", password: "Testpass1", confirmPassword: "Testpass1", role: "Client" };
  assert.equal(adminSetupSchema.safeParse(values).success, true);
  assert.equal(adminSetupSchema.parse(values).role, undefined);
  assert.equal(adminSetupSchema.safeParse({ ...values, confirmPassword: "different" }).success, false);
  assert.equal(adminSetupSchema.safeParse({ ...values, password: "short", confirmPassword: "short" }).success, false);
});

test("admin verification and password recovery retain dedicated sign-in links", () => {
  const verify = render(VerifyEmailForm, { admin: true, email: "", codeJustSent: false });
  assert.match(verify, /Verify administrator email/);
  assert.match(verify, /href="\/admin\/login"/);
  const forgot = render(ForgotPasswordForm, { admin: true });
  assert.match(forgot, /Recover administrator password/);
  assert.match(forgot, /href="\/admin\/login"/);
  assert.match(render(ResetPasswordForm, { admin: true, email: "" }), /href="\/admin\/login"/);
  assert.match(render(VerifyEmailForm, { email: "", codeJustSent: false }), /href="\/login"/);
  assert.match(render(ForgotPasswordForm), /href="\/login"/);
});

test("a signed-in ordinary participant sees a clear warning on admin sign-in", () => {
  globalThis.adminAuthTestState = { user: { roles: ["Client"] }, status: "authenticated" };
  assert.match(render(AdminLoginForm), /current account does not have administrator access/);
  globalThis.adminAuthTestState = undefined;
});

test("the admin guard never renders protected contents to ordinary roles", () => {
  for (const roles of [["Client"], ["Freelancer"], []]) {
    globalThis.adminAuthTestState = { user: { roles }, status: "authenticated" };
    assert.doesNotMatch(render(ProtectedRoute, { allowedRoles: ["Admin"], children: "Private administration" }), /Private administration/);
  }
  globalThis.adminAuthTestState = { user: { roles: ["Admin"] }, status: "authenticated" };
  assert.match(render(ProtectedRoute, { allowedRoles: ["Admin"], children: "Private administration" }), /Private administration/);
  globalThis.adminAuthTestState = undefined;
});


test("email MFA waits for a code and never embeds its challenge token in markup", () => {
  const html = render(AdminMfaForm, { prompt: { mfaRequired: true, challengeToken: "private-challenge-never-render", maskedEmail: "s***@gmail.com", expiresAt: "2026-10-18T20:00:00Z" }, onRestart() {} });
  assert.match(html, /Verify administrator sign-in/);
  assert.ok(html.includes("s***@gmail.com"));
  assert.match(html, /Email sign-in code/);
  assert.match(html, /expires in 5 minutes/);
  assert.doesNotMatch(html, /private-challenge-never-render/);
});
