import type { Metadata } from "next";
import { ResetPasswordForm } from "@/features/auth/components/reset-password-form";

export const metadata: Metadata = { title: "Reset administrator password", robots: { index: false, follow: false } };
export default async function AdminResetPasswordPage({ searchParams }: { searchParams: Promise<{ email?: string | string[] }> }) {
  const { email } = await searchParams;
  return <ResetPasswordForm admin email={typeof email === "string" ? email : ""} />;
}
