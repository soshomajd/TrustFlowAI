import type { ReactNode } from "react";

import { ProtectedRoute } from "@/features/auth/components/protected-route";

type ReviewLayoutProps = {
    children: ReactNode;
};

export default function ReviewLayout({ children }: ReviewLayoutProps) {
    return (
        <ProtectedRoute allowedRoles={["Admin"]}>{children}</ProtectedRoute>
    );
}
