import type { Metadata } from "next";
import { AdminProjectsScreen } from "@/features/admin/components/admin-projects-screen";

export const metadata: Metadata = { title: "Admin projects" };

export default function AdminProjectsPage() {
  return <AdminProjectsScreen />;
}
