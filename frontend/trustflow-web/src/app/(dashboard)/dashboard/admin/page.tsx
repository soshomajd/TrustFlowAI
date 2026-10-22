import type { Metadata } from "next";
import { AdminOverviewScreen } from "@/features/admin/components/admin-overview-screen";

export const metadata: Metadata = { title: "Admin overview" };

export default function AdminPage() {
  return <AdminOverviewScreen />;
}
