import type { ReactNode } from "react";
import { ProtectedRoute } from "@/features/auth/components/protected-route";

export const dynamic = "force-dynamic";

export default function AdminLayout({ children }: { children: ReactNode }) {
  const preview = process.env.NODE_ENV === "development" && process.env.TRUSTFLOW_LOCAL_ADMIN_PREVIEW === "1";
  return <ProtectedRoute allowedRoles={["Admin"]}>
    {preview && <p role="status" className="border-b bg-amber-500/10 px-6 py-3 text-sm">Local preview · read-only access · no wallet required</p>}
    {children}
  </ProtectedRoute>;
}
