import { ClientProfileScreen } from "@/features/clients/components/client-profile-screen";

export default async function PublicClientProfilePage({ params }: { params: Promise<{ clientId: string }> }) {
  const { clientId } = await params;
  return <ClientProfileScreen clientId={clientId} />;
}
