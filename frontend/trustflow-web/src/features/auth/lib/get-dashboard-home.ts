import type { AuthRole } from "@/features/auth/types/auth";

export function getDashboardHome(roles: readonly AuthRole[]): string {
  if (roles.includes("Admin")) return "/dashboard/admin";
  if (roles.includes("Client")) return "/dashboard/client";
  if (roles.includes("Freelancer")) return "/dashboard/freelancer";
  return "/";
}
