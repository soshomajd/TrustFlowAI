import type { ReactNode } from "react";
import { ProtectedRoute } from "@/features/auth/components/protected-route";
import { LocalAdminSessionNotice } from "@/features/admin/components/local-admin-session-notice";

export const dynamic = "force-dynamic";

export default function AdminLayout({ children }: { children: ReactNode }) {
  return <ProtectedRoute allowedRoles={["Admin"]}>
    <LocalAdminSessionNotice />
    {children}
  </ProtectedRoute>;
}
