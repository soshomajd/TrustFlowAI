"use client";

import { ArrowLeft, RefreshCw } from "lucide-react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ClientProfileView } from "@/features/clients/components/client-profile-view";
import { useClientProfile } from "@/features/clients/hooks/use-client-profile";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { isValidGuid } from "@/lib/validation/is-valid-guid";
import { useAuthStore } from "@/stores/auth-store";

export function ClientProfileScreen({ clientId }: { clientId: string }) {
  const normalizedId = clientId.trim();
  const query = useClientProfile(normalizedId);
  const user = useAuthStore((state) => state.user);

  if (!isValidGuid(normalizedId)) return <Notice title="Client profile not found" message="The client identifier is invalid." />;
  if (query.isPending) return <Loading />;
  if (query.isError || !query.data) return <Notice title="Could not load client profile" message={getApiErrorMessage(query.error)} action={<Button onClick={() => void query.refetch()}><RefreshCw className="size-4" />Try again</Button>} />;

  return <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
    <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
      <Button asChild variant="ghost" className="-ml-3"><Link href={user?.roles.includes("Freelancer") ? "/dashboard/freelancer/marketplace" : "/dashboard/client"}><ArrowLeft className="size-4" />Back</Link></Button>
      {user?.id === normalizedId && <Button asChild variant="outline"><Link href="/dashboard/client/profile">Edit my profile</Link></Button>}
    </div>
    <ClientProfileView profile={query.data} />
  </main>;
}

function Loading() { return <main className="mx-auto max-w-6xl animate-pulse space-y-6 px-4 py-8 sm:px-6"><div className="h-56 rounded-2xl border bg-muted/30" /><div className="h-96 rounded-2xl border bg-muted/20" /></main>; }

function Notice({ title, message, action }: { title: string; message: string; action?: React.ReactNode }) {
  return <main className="mx-auto max-w-3xl px-4 py-10"><section className="rounded-2xl border border-dashed p-8 text-center"><h1 className="text-xl font-semibold">{title}</h1><p className="mt-2 text-sm text-muted-foreground">{message}</p>{action && <div className="mt-5 flex justify-center">{action}</div>}</section></main>;
}
