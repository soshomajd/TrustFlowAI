"use client";

import { isAxiosError } from "axios";
import { ArrowLeft, Pencil, RefreshCw } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { FreelancerProfileView } from "@/features/freelancers/components/freelancer-profile-view";
import { useFreelancerProfile } from "@/features/freelancers/hooks/use-freelancer-profile";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { isValidGuid } from "@/lib/validation/is-valid-guid";
import { useAuthStore } from "@/stores/auth-store";

export function FreelancerProfileScreen({ freelancerId }: { freelancerId: string }) {
  const query = useFreelancerProfile(freelancerId);
  const userId = useAuthStore((state) => state.user?.id);
  const router = useRouter();
  const missing = !isValidGuid(freelancerId) || (isAxiosError(query.error) && query.error.response?.status === 404);
  return (
    <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
      <div className="mb-6 flex items-center justify-between gap-3">
        <Button variant="ghost" onClick={() => router.back()}><ArrowLeft className="size-4" />Back</Button>
        {userId === freelancerId && <Button asChild variant="outline"><Link href="/dashboard/freelancer/profile"><Pencil className="size-4" />Edit profile</Link></Button>}
      </div>
      {missing ? <ProfileNotice title="Freelancer not found" message="This profile does not exist or belongs to a different account type." /> : query.isPending ? <ProfileLoading /> : query.isError ? (
        <ProfileNotice title="Could not load profile" message={getApiErrorMessage(query.error)}><Button variant="outline" onClick={() => void query.refetch()}><RefreshCw className="size-4" />Try again</Button></ProfileNotice>
      ) : <FreelancerProfileView profile={query.data} />}
    </main>
  );
}

export function ProfileLoading() {
  return <div role="status" aria-label="Loading profile" className="space-y-6"><div className="h-48 animate-pulse rounded-2xl border bg-muted/40" /><div className="h-72 animate-pulse rounded-2xl border bg-muted/30" /><span className="sr-only">Loading profile</span></div>;
}

export function ProfileNotice({ title, message, children }: { title: string; message: string; children?: React.ReactNode }) {
  return <div role="alert" className="rounded-2xl border bg-card/70 p-8 text-center"><h1 className="text-xl font-semibold">{title}</h1><p className="my-4 text-sm text-muted-foreground">{message}</p>{children}</div>;
}
