"use client";

import { useAuthStore } from "@/stores/auth-store";

export function LocalAdminSessionNotice() {
  const readOnly = useAuthStore((state) => state.readOnly);
  const localReview = useAuthStore((state) => state.localReview);
  if (!readOnly && !localReview) return null;
  return <p role="status" className="border-b bg-amber-500/10 px-6 py-3 text-sm">
    {readOnly
      ? "Local preview · read-only access · no wallet required"
      : "Local development review · decisions enabled · arbiter wallet signature required"}
  </p>;
}
