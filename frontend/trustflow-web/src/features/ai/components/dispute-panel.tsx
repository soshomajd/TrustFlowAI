"use client";
import { formatMoney, moneyText } from "@/lib/money/exact-money";

import {
  CheckCircle2,
  CircleHelp,
  LoaderCircle,
  MinusCircle,
  Scale,
  ShieldAlert,
  XCircle,
} from "lucide-react";
import { useState } from "react";

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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AiDemoNotice } from "@/features/ai/components/ai-demo-notice";
import { useAiStatus } from "@/features/ai/hooks/use-ai-status";
import {
  useMilestoneDisputes,
  useOpenDispute,
  useRequestDisputeRuling,
  useRespondToDispute,
} from "@/features/ai/hooks/use-milestone-disputes";
import type { CriterionStatus } from "@/features/ai/types/ai";
import type {
  Dispute,
  DisputeRuling,
  DisputeSide,
} from "@/features/ai/types/dispute";
import type { MilestoneStatus } from "@/features/milestones/types/milestone";

const MIN_STATEMENT = 20;
const MAX_STATEMENT = 2000;

const criterionIcon: Record<
  CriterionStatus,
  { label: string; icon: typeof CheckCircle2; tone: string }
> = {
  met: { label: "Met", icon: CheckCircle2, tone: "text-emerald-400" },
  partially_met: {
    label: "Partly met",
    icon: MinusCircle,
    tone: "text-amber-300",
  },
  not_met: { label: "Not met", icon: XCircle, tone: "text-red-400" },
  unclear: { label: "Unclear", icon: CircleHelp, tone: "text-slate-400" },
};

type DisputePanelProps = {
  projectId: string;
  milestoneId: string;
  milestoneStatus: MilestoneStatus;

  // True when this person could open a dispute right now: the delivery is
  // waiting and the escrow is funded.
  canOpen: boolean;
};

export function DisputePanel({
  projectId,
  milestoneId,
  milestoneStatus,
  canOpen,
}: DisputePanelProps) {
  const status = useAiStatus();

  const query = useMilestoneDisputes(
    projectId,
    milestoneId,
    status.data?.enabled === true &&
      milestoneStatus !== "Pending" &&
      milestoneStatus !== "InProgress",
  );

  if (!status.data?.enabled) {
    return null;
  }

  const disputes = query.data ?? [];
  const latest = disputes[0];
  const earlier = disputes.slice(1);

  const hasLive =
    latest !== undefined &&
    (latest.status === "Open" || latest.status === "Ruled");

  const waitingForDecision =
    milestoneStatus === "Submitted" || milestoneStatus === "Rejected";

  if (!latest && !(canOpen && waitingForDecision)) {
    return null;
  }

  return (
    <section className="mt-5 rounded-xl border border-border/80 bg-background/30 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Scale className="size-4 text-electric" />

          <h4 className="text-sm font-semibold">Dispute</h4>
        </div>

        {!hasLive && canOpen && waitingForDecision && (
          <OpenDisputeDialog
            projectId={projectId}
            milestoneId={milestoneId}
          />
        )}
      </div>

      {!latest && (
        <p className="mt-2 text-xs leading-5 text-muted-foreground">
          If you and the other side cannot agree about this delivery, you can
          open a dispute. An AI reads the request, the delivered work and what
          each side writes, and gives an opinion on who is right.
        </p>
      )}

      {latest && (
        <DisputeCard
          dispute={latest}
          projectId={projectId}
          milestoneId={milestoneId}
        />
      )}

      {earlier.length > 0 && (
        <details className="mt-4 rounded-lg border bg-background/20 p-3">
          <summary className="cursor-pointer text-xs font-medium">
            Earlier disputes ({earlier.length})
          </summary>

          <div className="mt-3 space-y-3">
            {earlier.map((dispute) => (
              <DisputeCard
                key={dispute.id}
                dispute={dispute}
                projectId={projectId}
                milestoneId={milestoneId}
              />
            ))}
          </div>
        </details>
      )}
    </section>
  );
}

function OpenDisputeDialog({
  projectId,
  milestoneId,
}: {
  projectId: string;
  milestoneId: string;
}) {
  const [open, setOpen] = useState(false);
  const [statement, setStatement] = useState("");
  const mutation = useOpenDispute(projectId, milestoneId);

  const tooShort = statement.trim().length < MIN_STATEMENT;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);

        if (!next) {
          setStatement("");
          mutation.reset();
        }
      }}
    >
      <DialogTrigger asChild>
        <Button type="button" size="sm" variant="outline">
          Open a dispute
        </Button>
      </DialogTrigger>

      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Open a dispute</DialogTitle>

          <DialogDescription>
            Use this only if you could not agree with the other side.
          </DialogDescription>
        </DialogHeader>

        <ol className="list-decimal space-y-1 pl-5 text-sm leading-6 text-muted-foreground">
          <li>You write your side here.</li>

          <li>The other side gets time to write theirs.</li>

          <li>
            An AI reads the milestone request, the delivered work and both
            statements. Nobody can chat with it or give it more later.
          </li>

          <li>
            It gives an opinion. The opinion does not move money by itself.
            If you still cannot agree, either of you can start a 14 day
            request on the escrow, and then a human reviewer decides.
          </li>
        </ol>

        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <Label htmlFor="dispute-statement">Your side</Label>

            <span className="text-xs text-muted-foreground">
              {statement.length}/{MAX_STATEMENT}
            </span>
          </div>

          <Textarea
            id="dispute-statement"
            rows={6}
            maxLength={MAX_STATEMENT}
            value={statement}
            disabled={mutation.isPending}
            onChange={(event) => setStatement(event.target.value)}
            placeholder="Say what you asked for or delivered, and why you think it was or was not enough. Stick to facts. You write this once."
          />

          <p className="text-xs leading-5 text-muted-foreground">
            At least {MIN_STATEMENT} characters. While a dispute is open, the
            client cannot ask for changes and the freelancer cannot restart the
            milestone.
          </p>
        </div>

        <DialogFooter>
          <Button
            type="button"
            disabled={tooShort || mutation.isPending}
            onClick={() =>
              mutation.mutate(statement.trim(), {
                onSuccess: () => {
                  setOpen(false);
                  setStatement("");
                },
              })
            }
          >
            {mutation.isPending ? (
              <>
                <LoaderCircle className="size-4 animate-spin" />
                Opening
              </>
            ) : (
              "Open dispute"
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function sideName(side: DisputeSide, mine: DisputeSide) {
  return side === mine ? `${side} (you)` : side;
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

const statusStyle: Record<Dispute["status"], string> = {
  Open: "border-amber-500/30 bg-amber-500/10 text-amber-200",
  Ruled: "border-emerald-500/30 bg-emerald-500/10 text-emerald-300",
  Inconclusive: "border-slate-500/30 bg-slate-500/10 text-slate-300",
  Settled: "border-blue-500/30 bg-blue-500/10 text-blue-300",
};

const statusLabel: Record<Dispute["status"], string> = {
  Open: "Open",
  Ruled: "Ruled",
  Inconclusive: "No decision",
  Settled: "Closed",
};

function DisputeCard({
  dispute,
  projectId,
  milestoneId,
}: {
  dispute: Dispute;
  projectId: string;
  milestoneId: string;
}) {
  const otherSide: DisputeSide =
    dispute.openedByRole === "Client" ? "Freelancer" : "Client";

  return (
    <article className="mt-4 space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <span
          className={`rounded-full border px-2.5 py-1 text-xs ${statusStyle[dispute.status]}`}
        >
          {statusLabel[dispute.status]}
        </span>

        <span className="text-xs text-muted-foreground">
          Opened {formatDateTime(dispute.createdAt)}
        </span>
      </div>

      <AiDemoNotice isDemo={dispute.isDemo} />

      <div className="grid gap-3 md:grid-cols-2">
        <Statement
          title={`${sideName(dispute.openedByRole, dispute.myRole)} opened this dispute`}
          text={dispute.openerStatement}
        />

        <Statement
          title={`${sideName(otherSide, dispute.myRole)} answered`}
          text={dispute.respondentStatement}
          emptyText={
            dispute.status === "Open"
              ? `No answer yet. The ${otherSide} can still write one until ${formatDateTime(dispute.responseDeadline)}.`
              : `The ${otherSide} wrote nothing.`
          }
        />
      </div>

      {dispute.canRespond && (
        <RespondForm
          disputeId={dispute.id}
          projectId={projectId}
          milestoneId={milestoneId}
        />
      )}

      {dispute.status === "Open" && (
        <RulingRequest
          dispute={dispute}
          projectId={projectId}
          milestoneId={milestoneId}
        />
      )}

      {dispute.status === "Ruled" && dispute.ruling && (
        <RulingView ruling={dispute.ruling} mine={dispute.myRole} />
      )}

      {dispute.status === "Inconclusive" && (
        <div className="rounded-lg border border-slate-500/30 bg-slate-500/10 p-3 text-sm leading-6 text-slate-300">
          The AI could not decide on the evidence it had, so it did not give a
          ruling.
          {dispute.ruling?.summary && (
            <span className="mt-1 block text-xs text-slate-400">
              {dispute.ruling.summary}
            </span>
          )}
          <span className="mt-1 block text-xs text-slate-400">
            You can settle this between you, or open a new dispute.
          </span>
        </div>
      )}

      {dispute.status === "Settled" && (
        <p className="rounded-lg border border-blue-500/30 bg-blue-500/10 p-3 text-sm text-blue-200">
          This dispute is closed because the milestone was approved.
        </p>
      )}

      <p className="text-xs leading-5 text-muted-foreground">{dispute.notice}</p>
    </article>
  );
}

function Statement({
  title,
  text,
  emptyText,
}: {
  title: string;
  text: string | null;
  emptyText?: string;
}) {
  return (
    <div className="rounded-lg border bg-background/35 p-3">
      <p className="text-xs font-semibold">{title}</p>

      <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
        {text ?? emptyText}
      </p>
    </div>
  );
}

function RespondForm({
  disputeId,
  projectId,
  milestoneId,
}: {
  disputeId: string;
  projectId: string;
  milestoneId: string;
}) {
  const [statement, setStatement] = useState("");
  const mutation = useRespondToDispute(projectId, milestoneId);

  return (
    <div className="space-y-2 rounded-lg border border-amber-500/30 bg-amber-500/5 p-3">
      <Label htmlFor={`respond-${disputeId}`}>Write your side</Label>

      <Textarea
        id={`respond-${disputeId}`}
        rows={5}
        maxLength={MAX_STATEMENT}
        value={statement}
        disabled={mutation.isPending}
        onChange={(event) => setStatement(event.target.value)}
        placeholder="Answer what the other side wrote. Stick to facts. You can write this only once."
      />

      <div className="flex items-center justify-between gap-3">
        <span className="text-xs text-muted-foreground">
          {statement.length}/{MAX_STATEMENT}, at least {MIN_STATEMENT}
        </span>

        <Button
          type="button"
          size="sm"
          disabled={
            statement.trim().length < MIN_STATEMENT || mutation.isPending
          }
          onClick={() =>
            mutation.mutate({ disputeId, statement: statement.trim() })
          }
        >
          {mutation.isPending && (
            <LoaderCircle className="size-3.5 animate-spin" />
          )}
          Send my side
        </Button>
      </div>
    </div>
  );
}

function RulingRequest({
  dispute,
  projectId,
  milestoneId,
}: {
  dispute: Dispute;
  projectId: string;
  milestoneId: string;
}) {
  const mutation = useRequestDisputeRuling(projectId, milestoneId);

  if (!dispute.canRequestRuling) {
    return (
      <p className="text-xs leading-5 text-muted-foreground">
        You can ask the AI for a ruling once the other side has written their
        side, or after {formatDateTime(dispute.responseDeadline)}.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      <AlertDialog>
        <AlertDialogTrigger asChild>
          <Button type="button" size="sm" disabled={mutation.isPending}>
            {mutation.isPending ? (
              <>
                <LoaderCircle className="size-3.5 animate-spin" />
                The AI is reading
              </>
            ) : (
              <>
                <Scale className="size-3.5" />
                Ask the AI for a ruling
              </>
            )}
          </Button>
        </AlertDialogTrigger>

        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Ask the AI for a ruling?</AlertDialogTitle>

            <AlertDialogDescription>
              The AI will read the milestone request, the delivered work and
              both statements. A ruling is made once and nobody can change it
              or add to the evidence afterwards. It uses one of your daily AI
              requests. The ruling is an opinion and does not move any
              money by itself.
            </AlertDialogDescription>
          </AlertDialogHeader>

          <AlertDialogFooter>
            <AlertDialogCancel>Not yet</AlertDialogCancel>

            <AlertDialogAction onClick={() => mutation.mutate(dispute.id)}>
              Ask for the ruling
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {mutation.isPending && (
        <p className="text-xs text-muted-foreground">
          This can take up to a minute. You can leave this page open.
        </p>
      )}
    </div>
  );
}

function RulingView({
  ruling,
  mine,
}: {
  ruling: DisputeRuling;
  mine: DisputeSide;
}) {
  const headline =
    ruling.verdict === "FavorFreelancer"
      ? "The AI rules for the freelancer"
      : ruling.verdict === "FavorClient"
        ? "The AI rules for the client"
        : "The AI rules for a split";

  return (
    <div className="space-y-4 rounded-lg border border-emerald-500/20 bg-emerald-500/5 p-4">
      <div>
        <p className="text-sm font-semibold">{headline}</p>

        <p className="mt-1 text-xs text-muted-foreground">
          Confidence {Math.round(ruling.confidence * 100)}%. Ruled{" "}
          {formatDateTime(ruling.ruledAt)}.
        </p>
      </div>

      <div>
        <div
          className="flex h-3 overflow-hidden rounded-full bg-muted"
          role="img"
          aria-label={`Freelancer ${ruling.freelancerSharePercent} percent, client ${ruling.clientSharePercent} percent`}
        >
          <div
            className="bg-emerald-500"
            style={{ width: `${ruling.freelancerSharePercent}%` }}
          />

          <div
            className="bg-sky-500"
            style={{ width: `${ruling.clientSharePercent}%` }}
          />
        </div>

        <div className="mt-2 flex justify-between gap-3 text-xs">
          <span>
            Freelancer {ruling.freelancerSharePercent}% ·{" "}
            {formatMoney(moneyText(ruling, "freelancerAmount"))}
          </span>

          <span>
            Client {ruling.clientSharePercent}% ·{" "}
            {formatMoney(moneyText(ruling, "clientAmount"))}
          </span>
        </div>
      </div>

      <p className="text-sm leading-6 text-muted-foreground">{ruling.summary}</p>

      {ruling.findings.length > 0 && (
        <ul className="space-y-2">
          {ruling.findings.map((finding) => {
            const meta = criterionIcon[finding.status];
            const Icon = meta.icon;

            return (
              <li
                key={finding.requirement}
                className="rounded-lg border bg-background/35 p-3"
              >
                <p className="flex items-start gap-2 text-sm font-medium">
                  <Icon
                    className={`mt-0.5 size-4 shrink-0 ${meta.tone}`}
                    aria-hidden
                  />

                  <span>
                    {finding.requirement}
                    <span className={`ml-2 text-xs ${meta.tone}`}>
                      {meta.label}
                    </span>
                  </span>
                </p>

                {finding.evidence && (
                  <p className="mt-1 pl-6 text-xs leading-5 text-muted-foreground">
                    {finding.evidence}
                  </p>
                )}
              </li>
            );
          })}
        </ul>
      )}

      <details className="rounded-lg border bg-background/20 p-3">
        <summary className="cursor-pointer text-xs font-medium">
          How the AI got there
        </summary>

        <p className="mt-2 whitespace-pre-wrap text-xs leading-5 text-muted-foreground">
          {ruling.reasoning}
        </p>
      </details>

      {ruling.manipulationAttempts.length > 0 && (
        <div className="flex gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs leading-5 text-red-200">
          <ShieldAlert className="mt-0.5 size-4 shrink-0" />

          <div>
            <p className="font-semibold">
              The AI noticed an attempt to steer it
            </p>

            <ul className="mt-1 list-disc pl-4">
              {ruling.manipulationAttempts.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </div>
        </div>
      )}

      <p className="text-xs leading-5 text-muted-foreground">
        {mine === "Client"
          ? ruling.verdict === "FavorClient"
            ? "You can ask for changes on this milestone again, or settle it with the freelancer."
            : "If you agree, approve the milestone to release the payment from your wallet."
          : "The client still has to release the payment from their own wallet. You can talk to them about this ruling."}
      </p>
    </div>
  );
}
