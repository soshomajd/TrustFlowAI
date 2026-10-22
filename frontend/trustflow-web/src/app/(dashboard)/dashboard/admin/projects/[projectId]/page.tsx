import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { AdminProjectDossierScreen } from "@/features/admin/components/admin-project-dossier-screen";

export const metadata: Metadata = { title: "Project dossier", robots: { index: false, follow: false } };
export default async function AdminProjectDossierPage({ params }: { params: Promise<{ projectId: string }> }) {
  const { projectId } = await params;
  if (!/^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(projectId)) notFound();
  return <AdminProjectDossierScreen projectId={projectId} />;
}
