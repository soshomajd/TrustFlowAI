"use client";

import { Menu, ShieldCheck, X } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

import { navLinks } from "@/features/landing/content";
import { useAuthStore } from "@/stores/auth-store";

export function LandingHeader() {
  const [open, setOpen] = useState(false);
  const signedIn = useAuthStore((state) => state.status === "authenticated");

  // Escape closes the mobile menu, as people expect from any overlay.
  useEffect(() => {
    if (!open) {
      return;
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
      }
    }

    window.addEventListener("keydown", onKeyDown);

    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open]);

  return (
    <header className="sticky top-0 z-40 border-b border-border/60 bg-background/80 backdrop-blur-xl">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6">
        <Link
          href="/"
          className="flex min-h-11 items-center gap-2.5 rounded-lg font-semibold tracking-tight outline-none focus-visible:ring-3 focus-visible:ring-ring/60"
        >
          <span
            aria-hidden="true"
            className="flex size-9 items-center justify-center rounded-xl border border-primary/30 bg-primary/10 text-electric"
          >
            <ShieldCheck className="size-5" />
          </span>
          TrustFlow AI
        </Link>

        <nav aria-label="Main" className="hidden items-center gap-1 md:flex">
          {navLinks.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="flex min-h-11 items-center rounded-lg px-3 text-sm text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none"
            >
              {link.label}
            </a>
          ))}
        </nav>

        <div className="flex items-center gap-2">
          {!signedIn && (
            <Link
              href="/login"
              className="hidden min-h-11 items-center rounded-lg px-3 text-sm font-medium transition-colors hover:text-electric focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none sm:flex"
            >
              Sign in
            </Link>
          )}

          <Link
            href={signedIn ? "/dashboard" : "/register"}
            className="flex min-h-11 items-center rounded-xl bg-primary px-4 text-sm font-semibold text-primary-foreground shadow-blue-glow transition-colors hover:bg-primary/85 focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none"
          >
            {signedIn ? "Open dashboard" : "Get started"}
          </Link>

          <button
            type="button"
            aria-label={open ? "Close menu" : "Open menu"}
            aria-expanded={open}
            aria-controls="landing-mobile-menu"
            onClick={() => setOpen((value) => !value)}
            className="flex size-11 cursor-pointer items-center justify-center rounded-lg border border-border md:hidden focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none"
          >
            {open ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>
        </div>
      </div>

      {open && (
        <nav
          id="landing-mobile-menu"
          aria-label="Main"
          className="border-t border-border/60 bg-background/95 px-4 py-3 md:hidden"
        >
          <ul className="space-y-1">
            {navLinks.map((link) => (
              <li key={link.href}>
                <a
                  href={link.href}
                  onClick={() => setOpen(false)}
                  className="flex min-h-11 items-center rounded-lg px-3 text-base text-foreground/90 hover:bg-muted"
                >
                  {link.label}
                </a>
              </li>
            ))}

            <li>
              <Link
                href="/login"
                className="flex min-h-11 items-center rounded-lg px-3 text-base font-medium hover:bg-muted"
              >
                Sign in
              </Link>
            </li>
          </ul>
        </nav>
      )}
    </header>
  );
}
