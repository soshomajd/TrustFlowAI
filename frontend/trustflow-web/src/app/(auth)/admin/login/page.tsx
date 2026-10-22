import type { Metadata } from "next";
import { AdminLoginForm } from "@/features/admin/components/admin-login-form";
import { LocalAdminPreview } from "@/features/admin/components/local-admin-preview";

export const metadata: Metadata = { title: "Administrator sign in", robots: { index: false, follow: false } };

export default async function AdminLoginPage({ searchParams }: { searchParams: Promise<{ next?: string | string[] }> }) {
  const { next } = await searchParams;
  // Temporary local entry. The normal password/email/MFA form stays intact.
  if (process.env.NODE_ENV === "development" && process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW === "1")
    return <LocalAdminPreview next={typeof next === "string" ? next : undefined} />;
  return <AdminLoginForm next={typeof next === "string" ? next : undefined} />;
}
