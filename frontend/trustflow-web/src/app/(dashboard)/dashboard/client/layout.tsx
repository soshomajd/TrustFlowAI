import type { ReactNode } from "react";

import { ProtectedRoute } from
    "@/features/auth/components/protected-route";

type ClientLayoutProps = {
    children: ReactNode;
};

export default function ClientLayout({
    children,
}: ClientLayoutProps) {
    return (
        <ProtectedRoute
            allowedRoles={["Client"]}
        >
            {children}
        </ProtectedRoute>
    );
}