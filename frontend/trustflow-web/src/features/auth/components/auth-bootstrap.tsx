"use client";

import { useEffect } from "react";

import { restoreSession } from
    "@/features/auth/services/restore-session";
import { useAuthStore } from
    "@/stores/auth-store";

export function AuthBootstrap() {
    const status = useAuthStore(
        (state) => state.status,
    );

    useEffect(() => {
        /*
         * "checking" ro ham ghabool mikonim
         * ta agar Fast Refresh state ro rooye
         * checking negah dasht, restore dobare
         * shoroo beshe.
         */
        if (
            status !== "idle" &&
            status !== "checking"
        ) {
            return;
        }

        void restoreSession().catch(
            () => undefined,
        );
    }, [status]);

    return null;
}