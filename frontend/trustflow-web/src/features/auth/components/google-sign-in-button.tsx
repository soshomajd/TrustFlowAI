"use client";

import { BriefcaseBusiness, LoaderCircle, UserRoundSearch } from "lucide-react";
import Script from "next/script";
import { useCallback, useEffect, useRef, useState } from "react";

import { Button } from "@/components/ui/button";
import {
  isRoleRequiredError,
  useGoogleSignIn,
} from "@/features/auth/hooks/use-google-sign-in";
import type { AuthRole } from "@/features/auth/types/auth";
import "@/features/auth/types/google-identity";
import { env } from "@/config/env";

type SignUpRole = Exclude<AuthRole, "Admin">;

type GoogleSignInButtonProps = {
  // Supplied by the register form, which already asks for an account type.
  // The login form leaves it out and lets the role picker below handle it.
  role?: SignUpRole;
};

export function GoogleSignInButton({ role }: GoogleSignInButtonProps) {
  const clientId = env.googleClientId;

  const containerRef = useRef<HTMLDivElement>(null);
  const [isScriptReady, setIsScriptReady] = useState(false);
  const [pendingCredential, setPendingCredential] = useState<string | null>(
    null,
  );

  const signInMutation = useGoogleSignIn();

  const submit = useCallback(
    (credential: string, chosenRole: SignUpRole | undefined) => {
      signInMutation.mutate(
        { credential, role: chosenRole },
        {
          onError: (error) => {
            if (isRoleRequiredError(error)) {
              setPendingCredential(credential);
            }
          },
        },
      );
    },
    [signInMutation],
  );

  // Google's callback is registered once, so it must not close over a stale
  // role or mutation. Both refs are updated after commit rather than during
  // render, which the React Compiler rules require.
  const latestSubmit = useRef(submit);
  const latestRole = useRef(role);

  useEffect(() => {
    latestSubmit.current = submit;
    latestRole.current = role;
  });

  useEffect(() => {
    const container = containerRef.current;

    if (!isScriptReady || !clientId || !container) {
      return;
    }

    const google = window.google;

    if (!google) {
      return;
    }

    google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => {
        latestSubmit.current(response.credential, latestRole.current);
      },
      cancel_on_tap_outside: true,
    });

    google.accounts.id.renderButton(container, {
      type: "standard",
      theme: "outline",
      size: "large",
      text: "continue_with",
      shape: "pill",
      logo_alignment: "center",
    });

    return () => {
      container.replaceChildren();
    };
  }, [isScriptReady, clientId]);

  if (!clientId) {
    return null;
  }

  return (
    <div className="space-y-4">
      <Script
        src="https://accounts.google.com/gsi/client"
        strategy="afterInteractive"
        onReady={() => {
          setIsScriptReady(true);
        }}
      />

      <div className="flex items-center gap-3">
        <span className="h-px flex-1 bg-border" />
        <span className="text-xs text-muted-foreground">or</span>
        <span className="h-px flex-1 bg-border" />
      </div>

      <div className="flex justify-center">
        <div ref={containerRef} />
      </div>

      {signInMutation.isPending && (
        <p className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
          <LoaderCircle className="size-4 animate-spin" />
          Signing you in
        </p>
      )}

      {pendingCredential && (
        <div className="rounded-xl border border-primary/25 bg-primary/5 p-4">
          <p className="text-sm font-medium">
            One more step: how will you use TrustFlow?
          </p>

          <p className="mt-1 text-xs text-muted-foreground">
            You cannot change this later, so pick the one you need.
          </p>

          <div className="mt-3 grid gap-2 sm:grid-cols-2">
            <Button
              type="button"
              variant="outline"
              disabled={signInMutation.isPending}
              onClick={() => {
                submit(pendingCredential, "Client");
              }}
            >
              <BriefcaseBusiness className="size-4" />
              I want to hire
            </Button>

            <Button
              type="button"
              variant="outline"
              disabled={signInMutation.isPending}
              onClick={() => {
                submit(pendingCredential, "Freelancer");
              }}
            >
              <UserRoundSearch className="size-4" />
              I want to work
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
