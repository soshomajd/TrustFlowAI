"use client";
import { moneyText } from "@/lib/money/exact-money";
import { formatMoney as formatAmount } from "@/lib/money/exact-money";

import { formatTokenPayment } from "@/features/escrow/lib/platform-fee";

import { Gavel, LoaderCircle } from "lucide-react";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import {
  useCaseAction,
  type CaseAction,
} from "@/features/escrow/hooks/use-case-action";
import { useEscrowCases } from "@/features/escrow/hooks/use-escrow-cases";
import type { EscrowCase, ProjectEscrow } from "@/features/escrow/types/escrow";
import type { MilestoneStatus } from "@/features/milestones/types/milestone";
import { useNow } from "@/lib/hooks/use-now";

const DAY_MS = 24 * 60 * 60 * 1000;

type EscrowCasePanelProps = {
  projectId: string;
  viewerRole: "Client" | "Freelancer";
  escrow: ProjectEscrow | null;

  milestone: {
    id: string;
    status: MilestoneStatus;
    deadline: string;
    amount: number;
    paidAmount: number | null;
  };

  // Position of this milestone among its freelancer's stages.
  onChainIndex: number;

  // True when every earlier stage of this freelancer is finished. The
  // contract only ever looks at the next unpaid one.
  isNextInLine: boolean;
};

// The last resort when two people cannot agree: either side starts a 14 day
// clock on the contract, and after it a person (the reviewer) can decide how
// the milestone is shared. Both sides keep their normal moves while it runs.
export function EscrowCasePanel({
  projectId,
  viewerRole,
  escrow,
  milestone,
  onChainIndex,
  isNextInLine,
}: EscrowCasePanelProps) {
  const casesQuery = useEscrowCases(projectId);

  const now = useNow();

  const action = useCaseAction(projectId, escrow?.freelancerId ?? "");

  if (!escrow || !escrow.contractAddress) {
    return null;
  }

  const forThisMilestone = (casesQuery.data ?? []).filter(
    (item) => item.milestoneId === milestone.id,
  );

  const open = forThisMilestone.find((item) => item.status === "Open");

  const settled = forThisMilestone.find((item) => item.status === "Settled");

  const anyOpenOnEscrow = (casesQuery.data ?? []).some(
    (item) => item.escrowId === escrow.id && item.status === "Open",
  );

  const deadlinePassed = new Date(milestone.deadline).getTime() < now;

  const delivered =
    milestone.status === "Submitted" || milestone.status === "Rejected";

  const finished =
    milestone.status === "Approved" || milestone.status === "Settled";

  // Who may start one, and when. The contract allows more; this keeps the
  // button to cases where it makes sense for a person to press it.
  const canOpen =
    escrow.status === "Funded" &&
    !finished &&
    isNextInLine &&
    !anyOpenOnEscrow &&
    (viewerRole === "Freelancer"
      ? delivered
      : delivered ||
        (deadlinePassed &&
          (milestone.status === "Pending" ||
            milestone.status === "InProgress")));

  const run = (variables: { action: CaseAction }) =>
    action.mutate({ escrow, onChainIndex, ...variables });

  if (!open && !settled && !canOpen) {
    return null;
  }

  return (
    <section className="mt-5 rounded-xl border border-border/80 bg-background/30 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Gavel className="size-4 text-electric" />

          <h4 className="text-sm font-semibold">
            {viewerRole === "Freelancer"
              ? "Not paid yet?"
              : "Not delivered, or not good enough?"}
          </h4>
        </div>

        {!open && canOpen && (
          <OpenCaseDialog
            viewerRole={viewerRole}
            pending={action.isPending}
            onConfirm={() =>
              run({
                action:
                  viewerRole === "Freelancer"
                    ? "requestPayment"
                    : "requestRefund",
              })
            }
          />
        )}
      </div>

      {!open && !settled && canOpen && (
        <p className="mt-2 text-xs leading-5 text-muted-foreground">
          {viewerRole === "Freelancer"
            ? "If the client does not answer, you can ask for payment on the contract. A person can then decide after 14 days."
            : "If the work was not delivered or does not match the request, you can ask for the money back on the contract. A person can then decide after 14 days."}{" "}
          Nothing moves until then, and you can take the request back.
        </p>
      )}

      {open && (
        <OpenCaseCard
          item={open}
          viewerRole={viewerRole}
          pending={action.isPending}
          onWithdraw={() => run({ action: "withdrawRequest" })}
          onSplit={() => run({ action: "settleStalledCase" })}
        />
      )}

      {!open && settled && <SettledCaseCard item={settled} />}

      {action.isPending && (
        <p className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
          <LoaderCircle className="size-3.5 animate-spin" />
          Confirm in your wallet, then wait for the contract.
        </p>
      )}
    </section>
  );
}

function OpenCaseDialog({
  viewerRole,
  pending,
  onConfirm,
}: {
  viewerRole: "Client" | "Freelancer";
  pending: boolean;
  onConfirm: () => void;
}) {
  const asking =
    viewerRole === "Freelancer" ? "payment" : "a refund";

  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button type="button" size="sm" variant="outline" disabled={pending}>
          {viewerRole === "Freelancer" ? "Ask for payment" : "Ask for refund"}
        </Button>
      </AlertDialogTrigger>

      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Ask for {asking}?</AlertDialogTitle>

          <AlertDialogDescription asChild>
            <div className="space-y-2 text-sm">
              <p>
                This sends a request to the escrow contract from your wallet.
                It costs a small network fee.
              </p>

              <ul className="list-disc space-y-1 pl-5">
                <li>
                  The other side is told, and keeps all their normal moves.
                  {viewerRole === "Freelancer"
                    ? " The client can still release payment."
                    : " The freelancer can still deliver or leave the project."}
                </li>

                <li>
                  After 14 days a human reviewer can decide how this
                  milestone is shared between you. They can only pay the
                  freelancer, the client, or split it.
                </li>

                <li>You can take the request back until then.</li>
              </ul>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>

        <AlertDialogFooter>
          <AlertDialogCancel>Not now</AlertDialogCancel>

          <AlertDialogAction onClick={onConfirm}>
            Send request
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function OpenCaseCard({
  item,
  viewerRole,
  pending,
  onWithdraw,
  onSplit,
}: {
  item: EscrowCase;
  viewerRole: "Client" | "Freelancer";
  pending: boolean;
  onWithdraw: () => void;
  onSplit: () => void;
}) {
  const now = useNow();
  const settleAt = new Date(item.settleableAt).getTime();
  const stallAt = new Date(item.stallableAt).getTime();

  const openedByMe = item.openedByRole === viewerRole;
  const waiting = now < settleAt;
  const daysLeft = Math.max(1, Math.ceil((settleAt - now) / DAY_MS));
  const canSplit = now >= stallAt;

  return (
    <article className="mt-3 space-y-3 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <span className="rounded-full border border-amber-500/30 bg-amber-500/10 px-2.5 py-1 text-xs text-amber-200">
          {item.kind === "PaymentRequest"
            ? "Payment requested"
            : "Refund requested"}
        </span>

        <span className="text-xs text-muted-foreground">
          by the {item.openedByRole.toLowerCase()}
          {openedByMe ? " (you)" : ""}, {formatDate(item.openedAt)}
        </span>
      </div>

      <p className="leading-6 text-muted-foreground">
        {waiting
          ? `A reviewer can decide in ${daysLeft} ${daysLeft === 1 ? "day" : "days"} (${formatDate(item.settleableAt)}).`
          : "The 14 days are over. The reviewer can now decide."}{" "}
        {openedByMe
          ? "You can take the request back at any time before that."
          : item.kind === "PaymentRequest"
            ? "You can still release payment yourself, which closes it."
            : "You can still deliver the work or leave the project."}
      </p>

      {item.reviewNote && (
        <div className="rounded-lg border bg-card/50 p-3">
          <p className="text-xs font-medium">Note from the reviewer</p>

          <p className="mt-1 whitespace-pre-wrap text-xs leading-5 text-muted-foreground">
            {item.reviewNote}
          </p>
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        {openedByMe && (
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={pending}
            onClick={onWithdraw}
          >
            Take request back
          </Button>
        )}

        {canSplit && (
          <SplitDialog pending={pending} onConfirm={onSplit} />
        )}
      </div>

      {!canSplit && !waiting && (
        <p className="text-xs leading-5 text-muted-foreground">
          If nobody settles it by {formatDate(item.stallableAt)}, either side
          can split the milestone 50/50 and end the escrow.
        </p>
      )}
    </article>
  );
}

function SplitDialog({
  pending,
  onConfirm,
}: {
  pending: boolean;
  onConfirm: () => void;
}) {
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button type="button" size="sm" variant="outline" disabled={pending}>
          Split 50/50 and end
        </Button>
      </AlertDialogTrigger>

      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Split this milestone 50/50?</AlertDialogTitle>

          <AlertDialogDescription>
            The reviewer has not decided for 90 days. This gives half of this
            milestone to the freelancer, returns the other half and all later
            milestones to the client, and ends the escrow. It cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>

        <AlertDialogFooter>
          <AlertDialogCancel>Not now</AlertDialogCancel>

          <AlertDialogAction onClick={onConfirm}>
            Split and end
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function SettledCaseCard({ item }: { item: EscrowCase }) {
  return (
    <article className="mt-3 space-y-2 text-sm">
      <span className="rounded-full border border-blue-500/30 bg-blue-500/10 px-2.5 py-1 text-xs text-blue-300">
        Settled by a reviewer
      </span>

      <p className="leading-6 text-muted-foreground">
        The freelancer received {formatTokenPayment(moneyText(item, item.freelancerNetPaid != null ? "freelancerNetPaid" : "paidToFreelancer"))} and{" "}
        {formatAmount(moneyText(item, "returnedToClient"))} went back to the client.
        {" "}Platform fee: {formatTokenPayment(moneyText(item, "platformFeePaid"))}.
        {item.escrowEnded
          ? " The escrow was ended and the later milestones went back to the client."
          : ""}
      </p>

      {item.reviewNote && (
        <div className="rounded-lg border bg-card/50 p-3">
          <p className="text-xs font-medium">Note from the reviewer</p>

          <p className="mt-1 whitespace-pre-wrap text-xs leading-5 text-muted-foreground">
            {item.reviewNote}
          </p>
        </div>
      )}
    </article>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en-US", { dateStyle: "medium" }).format(
    new Date(value),
  );
}
