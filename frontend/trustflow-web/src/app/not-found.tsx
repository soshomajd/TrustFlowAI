import { ArrowLeft } from "lucide-react";
import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "Page not found",
};

// Next sends this with a 404 status and keeps it out of search results.
export default function NotFound() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center px-4 py-16 text-center">
      <p className="text-sm font-semibold uppercase tracking-[0.25em] text-electric">
        Error 404
      </p>

      <h1 className="mt-4 text-balance text-4xl font-bold tracking-tight sm:text-5xl">
        We could not find that page
      </h1>

      <p className="mt-4 max-w-md text-pretty text-muted-foreground">
        The link may be old or mistyped. Nothing was lost, and no money moved.
      </p>

      <div className="mt-8 flex flex-col gap-3 sm:flex-row">
        <Link
          href="/"
          className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-primary px-6 text-base font-semibold text-primary-foreground shadow-blue-glow transition-colors hover:bg-primary/85 focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
          Back to the home page
        </Link>

        <Link
          href="/login"
          className="inline-flex min-h-12 items-center justify-center rounded-xl border border-border bg-card/60 px-6 text-base font-semibold transition-colors hover:border-primary/50 hover:bg-card focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none"
        >
          Sign in
        </Link>
      </div>
    </main>
  );
}
