import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { LocalAdminPreview } from "@/features/admin/components/local-admin-preview";

export const dynamic = "force-dynamic";
export const metadata: Metadata = {
  title: "Local administration",
  robots: { index: false, follow: false },
};

export default function LocalAdminPage() {
  if (process.env.NODE_ENV !== "development" || process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW !== "1") {
    notFound();
  }
  return <LocalAdminPreview />;
}
