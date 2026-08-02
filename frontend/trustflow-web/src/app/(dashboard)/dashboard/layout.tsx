import type { ReactNode } from "react";

import { DashboardShell } from
    "@/components/shared/dashboard-shell";
import { ProtectedRoute } from
    "@/features/auth/components/protected-route";

type DashboardLayoutProps = {
    children: ReactNode;
};

export default function DashboardLayout({
    children,
}: DashboardLayoutProps) {
    return (
        <ProtectedRoute>
            <DashboardShell>
                {children}
            </DashboardShell>
        </ProtectedRoute>
    );
}