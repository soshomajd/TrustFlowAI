export function adminCsp(nonce: string, development: boolean, apiUrl?: string, rpcUrl?: string): string {
  const origins = new Set(["'self'"]);
  for (const text of [apiUrl, rpcUrl]) {
    if (!text) continue;
    const url = new URL(text);
    if (!["http:", "https:"].includes(url.protocol)) throw new Error("Unsupported admin connection origin.");
    origins.add(url.origin);
  }
  if (development) { origins.add("ws://localhost:*"); origins.add("ws://127.0.0.1:*"); }
  return [
    "default-src 'self'", `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${development ? " 'unsafe-eval'" : ""}`,
    // Existing UI components use inline style attributes. Scripts still require a nonce.
    "style-src 'self' 'unsafe-inline'", "img-src 'self' data: blob:", "font-src 'self' data:",
    `connect-src ${[...origins].join(" ")}`, "object-src 'none'", "base-uri 'self'", "form-action 'self'", "frame-ancestors 'none'",
  ].join("; ");
}
