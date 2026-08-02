import type { ReactNode } from "react";

import { ProtectedRoute } from
    "@/features/auth/components/protected-route";

type FreelancerLayoutProps = {
    children: ReactNode;
};

export default function FreelancerLayout({
    children,
}: FreelancerLayoutProps) {
    return (
        <ProtectedRoute
            allowedRoles={["Freelancer"]}
        >
            {children}
        </ProtectedRoute>
    );
}