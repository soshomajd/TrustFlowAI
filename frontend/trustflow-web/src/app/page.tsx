import Link from "next/link";
import { Button } from "@/components/ui/button";


export default function HomePage() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-6 px-6">
      <div className="text-center">
        <p className="mb-3 text-sm font-medium uppercase tracking-[0.3em] text-electric">
          Secure freelance workflow
        </p>

        <h1 className="text-4xl font-bold tracking-tight sm:text-6xl">
          TrustFlow AI
        </h1>

        <p className="mx-auto mt-4 max-w-xl text-muted-foreground">
          Manage projects, proposals and
          milestone-based work securely.
        </p>
      </div>

      <Button
        asChild
        className="shadow-blue-glow"
      >
        <Link href="/login">
          Sign in
        </Link>
      </Button>
    </main>
  );
}