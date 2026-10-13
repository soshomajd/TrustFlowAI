import type { ReactNode } from "react";

// Fades a block in as it scrolls into view. This is plain CSS (see "reveal" in
// globals.css), not script, so the block is ordinary server-rendered HTML that
// is fully visible to search engines, to people without script, and to anyone
// who asks for reduced motion.
export function Reveal({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return <div className={`reveal ${className ?? ""}`}>{children}</div>;
}
