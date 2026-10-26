"use client";
import { formatMoney as formatAmount, moneyText } from "@/lib/money/exact-money";

import { Gavel, LoaderCircle } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { useReviewCases } from "@/features/review/hooks/use-review-cases";
import type { ReviewCaseSummary } from "@/features/review/types/review";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useNow } from "@/lib/hooks/use-now";
import { cn } from "@/lib/utils";

const DAY_MS = 24 * 60 * 60 * 1000;

export function ReviewDeskScreen() {
  const [tab, setTab] = useState<"open" | "closed">("open");
  const query = useReviewCases(tab);

  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6">
      <header className="flex items-start gap-3">
        <Gavel className="mt-1 size-6 text-electric" />

        <div>
          <h1 className="text-2xl font-semibold">Review desk</h1>

          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
            Cases that a client or a freelancer started on an escrow contract.
            After 14 days you can decide how the milestone is shared between
            them. You sign the decision from your own wallet; this site cannot
            move the money for you.
          </p>
        </div>
      </header>

      <div className="mt-6 flex gap-2" role="tablist">
        {(["open", "closed"] as const).map((value) => (
          <Button
            key={value}
            type="button"
            role="tab"
            aria-selected={tab === value}
            size="sm"
            variant={tab === value ? "default" : "outline"}
            onClick={() => setTab(value)}
          >
            {value === "open" ? "Open cases" : "Closed cases"}
          </Button>
        ))}
      </div>

      {query.isPending && (
        <p className="mt-8 flex items-center gap-2 text-sm text-muted-foreground">
          <LoaderCircle className="size-4 animate-spin" />
          Loading cases
        </p>
      )}

      {query.isError && (
        <p role="alert" className="mt-8 text-sm text-destructive">
          {getApiErrorMessage(query.error, "Cases could not be loaded.")}
        </p>
      )}

      {query.data && query.data.length === 0 && (
        <p className="mt-8 rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">
          {tab === "open" ? "No open cases." : "No closed cases yet."}
        </p>
      )}

      <ul className="mt-6 space-y-3">
        {query.data?.map((item) => (
          <li key={item.id}>
            <CaseRow item={item} />
          </li>
        ))}
      </ul>
    </main>
  );
}

function CaseRow({ item }: { item: ReviewCaseSummary }) {
  const now = useNow();

  const days = Math.ceil(
    (new Date(item.settleableAt).getTime() - now) / DAY_MS,
  );

  return (
    <Link
      href={`/dashboard/review/${item.id}`}
      className="block rounded-xl border bg-card/60 p-4 transition hover:border-primary/40 hover:bg-card"
    >
      <div className="flex flex-wrap items-center gap-2">
        <span
          className={cn(
            "rounded-full border px-2.5 py-1 text-xs",
            item.status === "Open" && item.isReady
              ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-300"
              : item.status === "Open"
                ? "border-amber-500/30 bg-amber-500/10 text-amber-200"
                : "border-blue-500/30 bg-blue-500/10 text-blue-300",
          )}
        >
          {item.status === "Open"
            ? item.isReady
              ? "Estimated window ended · open to check chain"
              : `Estimated wait: ${Math.max(days, 1)} ${days <= 1 ? "day" : "days"}`
            : item.status === "Settled"
              ? "Settled"
              : "Taken back"}
        </span>

        <span className="rounded-full border px-2.5 py-1 text-xs text-muted-foreground">
          {item.kind === "PaymentRequest"
            ? "Freelancer asks for payment"
            : "Client asks for a refund"}
        </span>

        {item.hasAdvice && (
          <span className="rounded-full border px-2.5 py-1 text-xs text-muted-foreground">
            AI advice ready
          </span>
        )}
      </div>

      <p className="mt-3 font-medium">{item.milestoneTitle}</p>

      <p className="text-sm text-muted-foreground">
        {item.projectTitle} · {formatAmount(moneyText(item, "milestoneAmount"))}
      </p>

      {item.status === "Settled" && (
        <p className="mt-2 text-xs text-muted-foreground">
          Freelancer received {formatAmount(moneyText(item, item.freelancerNetPaid != null ? "freelancerNetPaid" : "paidToFreelancer"))}, platform fee {formatAmount(moneyText(item, "platformFeePaid"))}, client
          got {formatAmount(moneyText(item, "returnedToClient"))} back
          {item.escrowEnded ? ", escrow ended" : ""}.
        </p>
      )}
    </Link>
  );
}
