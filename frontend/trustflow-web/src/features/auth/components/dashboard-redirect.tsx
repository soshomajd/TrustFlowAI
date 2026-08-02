"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

import { FullScreenLoader } from
    "@/components/shared/full-screen-loader";
import { useAuthStore } from
    "@/stores/auth-store";

export function DashboardRedirect() {
    const router = useRouter();

    const user = useAuthStore(
        (state) => state.user,
    );

    useEffect(() => {
        if (!user) {
            return;
        }

        if (user.roles.includes("Client")) {
            router.replace(
                "/dashboard/client",
            );

            return;
        }

        if (
            user.roles.includes("Freelancer")
        ) {
            router.replace(
                "/dashboard/freelancer",
            );

            return;
        }

        router.replace("/");
    }, [user, router]);

    return (
        <FullScreenLoader message="Opening your dashboard..." />
    );
}