import { NextRequest, NextResponse } from "next/server";
import { adminCsp } from "@/lib/security/admin-csp";

export function proxy(request: NextRequest) {
  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");
  const csp = adminCsp(nonce, process.env.NODE_ENV === "development", process.env.NEXT_PUBLIC_API_URL, process.env.NEXT_PUBLIC_RPC_URL);
  const headers = new Headers(request.headers);
  headers.set("x-nonce", nonce);
  headers.set("Content-Security-Policy", csp);
  const response = NextResponse.next({ request: { headers } });
  response.headers.set("Content-Security-Policy", csp);
  response.headers.set("X-Frame-Options", "DENY");
  response.headers.set("X-Content-Type-Options", "nosniff");
  response.headers.set("Referrer-Policy", "no-referrer");
  response.headers.set("Cache-Control", "private, no-store");
  if (process.env.NODE_ENV === "production") response.headers.set("Strict-Transport-Security", "max-age=31536000");
  return response;
}

export const config = { matcher: ["/admin/:path*", "/dashboard/admin/:path*", "/dashboard/review/:path*"] };
