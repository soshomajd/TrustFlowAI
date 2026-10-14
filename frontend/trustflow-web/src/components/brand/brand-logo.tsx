import Image from "next/image";

import { cn } from "@/lib/utils";

// The TrustFlow AI mark. Its blue shield sinks into the dark page, so it sits
// on a light tile where it can be read at a glance. Each file is drawn for one
// place and shown at its own size; they are small PNGs, served as they are.
export function HeaderLogo({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 rounded-xl bg-white p-1 shadow-cyan-glow",
        className,
      )}
    >
      <Image
        src="/trustflow-ai-header-40.png"
        alt=""
        width={40}
        height={40}
        unoptimized
      />
    </span>
  );
}

// Drawn at 64 pixels and shown at 32, so it stays sharp on dense screens.
export function FooterLogo({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 rounded-lg bg-white p-0.5",
        className,
      )}
    >
      <Image
        src="/trustflow-ai-footer-64.png"
        alt=""
        width={32}
        height={32}
        unoptimized
      />
    </span>
  );
}
