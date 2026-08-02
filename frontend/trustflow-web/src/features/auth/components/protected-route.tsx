"use client";

import { type ReactNode, useEffect, } from "react";
import { usePathname, useRouter, } from "next/navigation";
import { FullScreenLoader } from "@/components/shared/full-screen-loader";
import type { AuthRole } from "@/features/auth/types/auth";
import { useAuthStore } from "@/stores/auth-store";
import { AuthUnavailableState } from
    "@/features/auth/components/auth-unavailable-state";

type ProtectedRouteProps = {
    children: ReactNode;
    allowedRoles?: AuthRole[];
};

export function ProtectedRoute({
    children,
    allowedRoles,
}: ProtectedRouteProps) {
    const router = useRouter();
    const pathname = usePathname();

    const status = useAuthStore(
        (state) => state.status,
    );

    const user = useAuthStore(
        (state) => state.user,
    );

    const hasAllowedRole =
        !allowedRoles ||
        allowedRoles.some((role) =>
            user?.roles.includes(role),
        );

    useEffect(() => {
        if (status === "unauthenticated") {
            const nextPath =
                encodeURIComponent(pathname);

            router.replace(
                `/login?next=${nextPath}`,
            );

            return;
        }

        if (
            status === "authenticated" &&
            !hasAllowedRole
        ) {
            router.replace("/dashboard");
        }
    }, [
        status,
        hasAllowedRole,
        pathname,
        router,
    ]);
    if (status === "unavailable") {
        return <AuthUnavailableState />;
    }

    if (
        status === "idle" ||
        status === "checking"
    ) {
        return (
            <FullScreenLoader message="Restoring your secure session..." />
        );
    }

    if (
        status !== "authenticated" ||
        !user ||
        !hasAllowedRole
    ) {
        return (
            <FullScreenLoader message="Redirecting..." />
        );
    }

    return children;
}