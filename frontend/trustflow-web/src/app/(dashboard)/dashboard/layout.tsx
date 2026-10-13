import type { Metadata } from "next";
import type { ReactNode } from "react";

import { DashboardShell } from
    "@/components/shared/dashboard-shell";
import { AuthBootstrap } from
    "@/features/auth/components/auth-bootstrap";
import { ProtectedRoute } from
    "@/features/auth/components/protected-route";
import { AppProviders } from
    "@/providers/app-providers";

// Everything behind the sign-in is private. The page is drawn in the browser
// once the session is known, so there is nothing here for a search engine.
export const metadata: Metadata = {
    robots: {
        index: false,
        follow: false,
    },
};

type DashboardLayoutProps = {
    children: ReactNode;
};

export default function DashboardLayout({
    children,
}: DashboardLayoutProps) {
    return (
        <AppProviders>
            <AuthBootstrap />

            <ProtectedRoute>
                <DashboardShell>
                    {children}
                </DashboardShell>
            </ProtectedRoute>
        </AppProviders>
    );
}
