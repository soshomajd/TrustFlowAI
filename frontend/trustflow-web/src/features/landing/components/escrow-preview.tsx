import { CheckCircle2, CircleDashed, LockKeyhole, Timer } from "lucide-react";

// A picture of the product, not live data. It says so on the card, so nobody
// takes the names or amounts for a real project.
const milestones = [
  { title: "Design and menu page", amount: "$400", state: "released" },
  { title: "Booking form", amount: "$350", state: "delivered" },
  { title: "Launch and handover", amount: "$250", state: "waiting" },
] as const;

const stateLabel = {
  released: "Released",
  delivered: "Delivered, you review",
  waiting: "Locked in escrow",
} as const;

export function EscrowPreview({
  className,
  compact = false,
}: {
  className?: string;
  compact?: boolean;
}) {
  return (
    <figure
      aria-label="Example of a funded project escrow"
      className={className ?? "relative mx-auto w-full max-w-md"}
    >
      <div
        aria-hidden="true"
        className="absolute -inset-6 -z-10 rounded-[2rem] bg-linear-to-br from-primary/25 via-electric/10 to-transparent blur-2xl"
      />

      <div className="rounded-2xl border border-border bg-card/90 p-5 shadow-2xl shadow-black/40 backdrop-blur">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-xs font-medium uppercase tracking-widest text-muted-foreground">
              Example project
            </p>

            <p className="mt-1 text-lg font-semibold">Bakery website</p>
          </div>

          <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success/10 px-2.5 py-1 text-xs font-medium text-success">
            <LockKeyhole className="size-3.5" aria-hidden="true" />
            Funded
          </span>
        </div>

        {!compact && (
        <dl className="mt-5 grid grid-cols-2 gap-3 text-sm">
          <div className="rounded-xl border bg-background/50 p-3">
            <dt className="text-xs text-muted-foreground">In escrow</dt>

            <dd className="mt-1 text-xl font-semibold tabular-nums">$600</dd>
          </div>

          <div className="rounded-xl border bg-background/50 p-3">
            <dt className="text-xs text-muted-foreground">Released</dt>

            <dd className="mt-1 text-xl font-semibold tabular-nums">$400</dd>
          </div>
        </dl>
        )}

        <ol className="mt-5 space-y-2.5">
          {milestones.map((item) => (
            <li
              key={item.title}
              className="flex items-center gap-3 rounded-xl border bg-background/40 p-3"
            >
              {item.state === "released" ? (
                <CheckCircle2
                  className="size-5 shrink-0 text-success"
                  aria-hidden="true"
                />
              ) : item.state === "delivered" ? (
                <Timer
                  className="size-5 shrink-0 text-warning"
                  aria-hidden="true"
                />
              ) : (
                <CircleDashed
                  className="size-5 shrink-0 text-muted-foreground"
                  aria-hidden="true"
                />
              )}

              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{item.title}</p>

                <p className="text-xs text-muted-foreground">
                  {stateLabel[item.state]}
                </p>
              </div>

              <p className="text-sm font-semibold tabular-nums">
                {item.amount}
              </p>
            </li>
          ))}
        </ol>

        {!compact && (
        <div className="mt-5 flex items-center justify-between gap-3 rounded-xl border border-primary/30 bg-primary/10 p-3">
          <p className="text-xs leading-5 text-foreground/90">
            Happy with the delivery? Release this milestone from your own
            wallet.
          </p>

          <span className="shrink-0 rounded-lg bg-primary px-3 py-2 text-xs font-semibold text-primary-foreground">
            Release $350
          </span>
        </div>
        )}
      </div>

      <figcaption className="mt-3 text-center text-xs text-muted-foreground">
        Illustration. Names and amounts are made up.
      </figcaption>
    </figure>
  );
}
