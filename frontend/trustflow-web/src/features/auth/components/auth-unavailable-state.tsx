"use client";

import {
    RefreshCw,
    ServerCrash,
} from "lucide-react";

import { Button } from
    "@/components/ui/button";
import { restoreSession } from
    "@/features/auth/services/restore-session";

export function AuthUnavailableState() {
    function handleRetry() {
        void restoreSession().catch(
            () => undefined,
        );
    }

    return (
        <main className="flex min-h-screen items-center justify-center px-6">
            <div className="w-full max-w-md rounded-2xl border bg-card/80 p-8 text-center shadow-blue-glow backdrop-blur">
                <div className="mx-auto flex size-14 items-center justify-center rounded-2xl border border-destructive/30 bg-destructive/10">
                    <ServerCrash className="size-6 text-destructive" />
                </div>

                <h1 className="mt-5 text-xl font-semibold">
                    Server is unavailable
                </h1>

                <p className="mt-3 text-sm leading-6 text-muted-foreground">
                    TrustFlow could not connect to
                    the API. Your session has not
                    been marked as signed out.
                </p>

                <Button
                    type="button"
                    onClick={handleRetry}
                    className="mt-6"
                >
                    <RefreshCw className="size-4" />
                    Try again
                </Button>
            </div>
        </main>
    );
}