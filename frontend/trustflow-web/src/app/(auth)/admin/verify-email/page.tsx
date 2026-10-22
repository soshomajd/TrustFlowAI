import type { Metadata } from "next";
import { VerifyEmailForm } from "@/features/auth/components/verify-email-form";

export const metadata: Metadata = { title: "Verify administrator email", robots: { index: false, follow: false } };
export default async function AdminVerifyEmailPage({ searchParams }: { searchParams: Promise<{ email?: string | string[]; sent?: string | string[] }> }) {
  const { email, sent } = await searchParams;
  return <VerifyEmailForm admin email={typeof email === "string" ? email : ""} codeJustSent={sent === "true"} />;
}
