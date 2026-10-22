import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { getSignInPath, isAdminDashboardPath } from "@/features/auth/lib/sign-in-path";

import { LoginForm } from
    "@/features/auth/components/login-form";

export const metadata: Metadata = {
    title: "Sign in",
    description:
        "Sign in to TrustFlow AI to fund an escrow, deliver milestones or release payment from your own wallet.",
    alternates: {
        canonical: "/login",
    },
};

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string | string[] }> }) {
    const { next } = await searchParams;
    if (typeof next === "string" && isAdminDashboardPath(next)) redirect(getSignInPath(next));
    return <LoginForm />;
}
