import assert from "node:assert/strict";
import { test } from "node:test";
import { adminCsp } from "../src/lib/security/admin-csp.ts";

test("production CSP limits connections, scripts and framing", () => {
  const csp = adminCsp("testnonce", false, "https://api.example.com/api", "https://rpc.example.com/path");
  assert.match(csp, /script-src 'self' 'nonce-testnonce' 'strict-dynamic'/);
  assert.match(csp, /frame-ancestors 'none'/);
  assert.match(csp, /object-src 'none'/);
  assert.match(csp, /connect-src 'self' https:\/\/api.example.com https:\/\/rpc.example.com/);
  assert.doesNotMatch(csp, /unsafe-eval|ws:|api.example.com\/api/);
  assert.equal(csp.split(";").find(directive => directive.trim().startsWith("script-src")).includes("unsafe-inline"), false);
});

test("development supports HMR and each request nonce remains distinct", () => {
  assert.match(adminCsp("one", true, "http://localhost:5114/api"), /unsafe-eval/);
  assert.match(adminCsp("one", true), /ws:\/\/localhost:\*/);
  assert.notEqual(adminCsp("one", false), adminCsp("two", false));
});

test("CSP rejects non-HTTP API origins", () => {
  assert.throws(() => adminCsp("nonce", false, "javascript:alert(1)"));
});
