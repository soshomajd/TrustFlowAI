"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

import { FullScreenLoader } from
    "@/components/shared/full-screen-loader";
import { useAuthStore } from
    "@/stores/auth-store";
import { getDashboardHome } from "@/features/auth/lib/get-dashboard-home";

export function DashboardRedirect() {
    const router = useRouter();

    const user = useAuthStore(
        (state) => state.user,
    );

    useEffect(() => {
        if (!user) {
            return;
        }

        router.replace(getDashboardHome(user.roles));
    }, [user, router]);

    return (
        <FullScreenLoader message="Opening your dashboard..." />
    );
}
