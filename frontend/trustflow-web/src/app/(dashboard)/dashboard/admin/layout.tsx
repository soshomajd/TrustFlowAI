import type { ReactNode } from "react";
import { ProtectedRoute } from "@/features/auth/components/protected-route";

export default function AdminLayout({ children }: { children: ReactNode }) {
  return <ProtectedRoute allowedRoles={["Admin"]}>{children}</ProtectedRoute>;
}
