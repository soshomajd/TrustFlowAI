"use client";

import dynamic from "next/dynamic";

// The public pages only need to know whether someone is signed in to label one
// button. Loading the session code (the API client and its validation) in a
// separate chunk keeps it out of the script the first paint depends on.
const AuthBootstrap = dynamic(
  () =>
    import("@/features/auth/components/auth-bootstrap").then(
      (module) => module.AuthBootstrap,
    ),
  { ssr: false },
);

export function DeferredSessionCheck() {
  return <AuthBootstrap />;
}
