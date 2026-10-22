import type { Metadata } from "next";
import { AdminSetupForm } from "@/features/admin/components/admin-setup-form";

export const metadata: Metadata = { title: "Activate administrator access", robots: { index: false, follow: false } };
export default function AdminSetupPage() { return <AdminSetupForm />; }
