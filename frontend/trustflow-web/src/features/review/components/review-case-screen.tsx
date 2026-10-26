"use client";
import { formatMoney as formatAmount, moneyText, subtractMoney } from "@/lib/money/exact-money";

import { percentageSettlement } from "@/features/review/lib/settlement-amount";
import { useAuthStore } from "@/stores/auth-store";
import { decimalToBaseUnits } from "@/lib/money/decimal-amount";

import { previewPlatformFee, formatTokenPayment } from "@/features/escrow/lib/platform-fee";

import {
  ArrowLeft,
  CheckCircle2,
  CircleHelp,
  Download,
  ExternalLink,
  Gavel,
  LoaderCircle,
  MinusCircle,
  RefreshCw,
  XCircle,
} from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { useConnection } from "wagmi";

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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AiDemoNotice } from "@/features/ai/components/ai-demo-notice";
import type { CriterionStatus } from "@/features/ai/types/ai";
import type { MilestoneSubmission } from "@/features/milestones/types/milestone";
import { downloadReviewFile } from "@/features/review/api/review-api";
import {
  useAdviseReviewCase,
  useReviewCase,
  useSaveReviewNote,
  useSyncReviewCase,
} from "@/features/review/hooks/use-review-cases";
import { useSettleCase } from "@/features/review/hooks/use-settle-case";
import { useReviewCaseReadiness } from "@/features/review/hooks/use-review-case-readiness";
import { settlementReadinessMessage } from "@/features/review/lib/settlement-readiness";
import type { ReviewCaseDetail } from "@/features/review/types/review";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

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

export function ReviewCaseScreen({ caseId }: { caseId: string }) {
  const query = useReviewCase(caseId);

  if (query.isPending) {
    return (
      <main className="mx-auto max-w-4xl px-4 py-10">
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <LoaderCircle className="size-4 animate-spin" />
          Loading the case
        </p>
      </main>
    );
  }

  if (query.isError) {
    return (
      <main className="mx-auto max-w-4xl px-4 py-10">
        <p role="alert" className="text-sm text-destructive">
          {getApiErrorMessage(query.error, "The case could not be loaded.")}
        </p>
      </main>
    );
  }

  return <CaseView caseId={caseId} detail={query.data} />;
}

function CaseView({
  caseId,
  detail,
}: {
  caseId: string;
  detail: ReviewCaseDetail;
}) {
  const readOnly = useAuthStore((state) => state.readOnly);
  const open = detail.case.status === "Open";
  const sync = useSyncReviewCase(caseId);
  const readiness = useReviewCaseReadiness(detail);

  return (
    <main className="mx-auto max-w-4xl space-y-6 px-4 py-8 sm:px-6">
      <Button asChild variant="ghost" size="sm">
        <Link href="/dashboard/review">
          <ArrowLeft className="size-4" />
          Review desk
        </Link>
      </Button>

      <header className="space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <Gavel className="size-5 text-electric" />

          <h1 className="text-xl font-semibold">
            {detail.case.milestoneTitle}
          </h1>
        </div>

        <p className="text-sm text-muted-foreground">
          {detail.case.projectTitle} ·{" "}
          {formatAmount(moneyText(detail.case, "milestoneAmount"))} · milestone{" "}
          {detail.milestoneSequence}
        </p>

        <p className="text-xs text-muted-foreground">Case reference: <code className="break-all">{detail.case.referenceCode ?? "Not recorded"}</code></p>

        <p className="text-sm leading-6">
          {detail.case.kind === "PaymentRequest"
            ? "The freelancer says the work was delivered and asks to be paid."
            : "The client says the work was not delivered, or not good enough, and asks for the money back."}{" "}
          <span className="text-muted-foreground">
            Opened {formatDate(detail.case.openedAt)}.
            {open ? ` Recorded response deadline: ${formatDate(detail.case.settleableAt)}. Eligibility is checked against the chain below.` : ""}
          </span>
        </p>

        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={sync.isPending || readOnly}
          onClick={() => { sync.mutate(); void readiness.refetch(); }}
        >
          {sync.isPending ? (
            <LoaderCircle className="size-4 animate-spin" />
          ) : (
            <RefreshCw className="size-4" />
          )}
          Read the contract again
        </Button>
      </header>

      <Section title="What was asked">
        <Label className="text-xs text-muted-foreground">Project</Label>

        <p className="mt-1 whitespace-pre-wrap text-sm leading-6">
          {detail.projectDescription}
        </p>

        <Label className="mt-4 block text-xs text-muted-foreground">
          This milestone, as the client wrote it before the work
        </Label>

        <p className="mt-1 whitespace-pre-wrap text-sm leading-6">
          {detail.milestoneDescription}
        </p>

        <p className="mt-3 text-xs text-muted-foreground">
          Deadline {formatDate(detail.milestoneDeadline)}
          {detail.deadlinePassed ? " (passed)" : " (not passed yet)"}.
          Milestone status in the app: {detail.milestoneStatus}.
        </p>
      </Section>

      <Section title="What was delivered">
        {detail.deliveries.length === 0 ? (
          <p
            role="note"
            className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-sm text-amber-200"
          >
            Nothing was delivered on the platform for this milestone.
          </p>
        ) : (
          <div className="space-y-3">
            {detail.deliveries.map((delivery) => (
              <DeliveryCard
                key={delivery.id}
                caseId={caseId}
                delivery={delivery}
              />
            ))}
          </div>
        )}
      </Section>

      {detail.dispute && (
        <Section title="What each side wrote">
          <p className="text-xs text-muted-foreground">
            Opened by the {detail.dispute.openedByRole.toLowerCase()}.
          </p>

          <Statement
            who={detail.dispute.openedByRole}
            text={detail.dispute.openerStatement}
          />

          <Statement
            who={
              detail.dispute.openedByRole === "Client"
                ? "Freelancer"
                : "Client"
            }
            text={detail.dispute.respondentStatement}
          />

          {detail.dispute.rulingSummary && (
            <p className="mt-3 rounded-lg border bg-background/30 p-3 text-xs leading-5 text-muted-foreground">
              Earlier AI opinion asked for by one of them (
              {detail.dispute.rulingFreelancerSharePercent}% to the
              freelancer): {detail.dispute.rulingSummary}
            </p>
          )}
        </Section>
      )}

      {open && <AdviceSection caseId={caseId} detail={detail} />}

      {open ? (
        <DecisionSection caseId={caseId} detail={detail} readiness={readiness} />
      ) : (
        <ClosedSection detail={detail} />
      )}
    </main>
  );
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-2xl border bg-card/70 p-5">
      <h2 className="mb-3 text-base font-semibold">{title}</h2>

      {children}
    </section>
  );
}

function Statement({ who, text }: { who: string; text: string | null }) {
  return (
    <div className="mt-3 rounded-lg border bg-background/30 p-3">
      <p className="text-xs font-medium">{who}</p>

      <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
        {text ?? "Wrote nothing."}
      </p>
    </div>
  );
}

function DeliveryCard({
  caseId,
  delivery,
}: {
  caseId: string;
  delivery: MilestoneSubmission;
}) {
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function download(attachmentId: string, fileName: string) {
    setBusy(attachmentId);
    setError(null);

    try {
      const blob = await downloadReviewFile(caseId, attachmentId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");

      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      setError(
        getApiErrorMessage(downloadError, "The file could not be downloaded."),
      );
    } finally {
      setBusy(null);
    }
  }

  return (
    <article className="rounded-lg border bg-background/30 p-3">
      <p className="text-xs text-muted-foreground">
        Version {delivery.version} · {formatDate(delivery.submittedAt)}
      </p>

      <p className="mt-2 whitespace-pre-wrap text-sm leading-6">
        {delivery.note}
      </p>

      {(delivery.previewLink || delivery.sourceFilesLink) && (
        <div className="mt-3 flex flex-wrap gap-3 text-xs">
          {[delivery.previewLink, delivery.sourceFilesLink]
            .filter((link): link is string => Boolean(link))
            .map((link) => (
              <a
                key={link}
                href={link}
                target="_blank"
                rel="noopener noreferrer nofollow"
                className="inline-flex items-center gap-1 text-electric underline underline-offset-2"
              >
                <ExternalLink className="size-3" />
                {link}
              </a>
            ))}
        </div>
      )}

      {delivery.attachments.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
          {delivery.attachments.map((file) => (
            <Button
              key={file.id}
              type="button"
              variant="outline"
              size="sm"
              disabled={busy !== null}
              onClick={() => void download(file.id, file.fileName)}
            >
              {busy === file.id ? (
                <LoaderCircle className="size-3.5 animate-spin" />
              ) : (
                <Download className="size-3.5" />
              )}
              {file.fileName}
            </Button>
          ))}
        </div>
      )}

      {delivery.clientFeedback && (
        <p className="mt-3 text-xs leading-5 text-muted-foreground">
          Client feedback: {delivery.clientFeedback}
        </p>
      )}

      {error && (
        <p role="alert" className="mt-2 text-xs text-destructive">
          {error}
        </p>
      )}
    </article>
  );
}

function AdviceSection({
  caseId,
  detail,
}: {
  caseId: string;
  detail: ReviewCaseDetail;
}) {
  const readOnly = useAuthStore((state) => state.readOnly);
  const advise = useAdviseReviewCase(caseId);
  const advice = detail.advice;

  return (
    <Section title="AI advice">
      <p className="text-xs leading-5 text-muted-foreground">
        The AI reads the same evidence you see and suggests a split. It is
        advice only: it cannot sign, and you decide.
      </p>

      {advice && <AiDemoNotice isDemo={advice.isDemo} />}

      {advice && (
        <div className="mt-3 space-y-3 text-sm">
          <p>
            Suggests{" "}
            <strong>{advice.freelancerSharePercent}% to the freelancer</strong>{" "}
            ({formatAmount(
              percentageSettlement(moneyText(detail.case, "milestoneAmount"), String(advice.freelancerSharePercent), detail.tokenDecimals).freelancer,
            )}
            ), confidence {Math.round(advice.confidence * 100)}%.
          </p>

          <p className="leading-6 text-muted-foreground">{advice.summary}</p>

          <ul className="space-y-2">
            {advice.findings.map((finding) => {
              const style = criterionIcon[finding.status];
              const Icon = style.icon;

              return (
                <li key={finding.requirement} className="flex gap-2 text-xs">
                  <Icon className={`mt-0.5 size-4 shrink-0 ${style.tone}`} />

                  <span>
                    <strong>{style.label}:</strong> {finding.requirement}
                    {finding.evidence ? ` — ${finding.evidence}` : ""}
                  </span>
                </li>
              );
            })}
          </ul>

          <details className="text-xs">
            <summary className="cursor-pointer font-medium">Reasoning</summary>

            <p className="mt-2 whitespace-pre-wrap leading-5 text-muted-foreground">
              {advice.reasoning}
            </p>
          </details>

          {advice.manipulationAttempts.length > 0 && (
            <p className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-xs text-amber-200">
              The AI noticed an attempt to steer it:{" "}
              {advice.manipulationAttempts.join(" ")}
            </p>
          )}
        </div>
      )}

      {detail.aiEnabled ? (
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="mt-3"
          disabled={advise.isPending || readOnly}
          onClick={() => advise.mutate()}
        >
          {advise.isPending ? (
            <LoaderCircle className="size-4 animate-spin" />
          ) : null}
          {advice ? "Ask again" : "Ask the AI for advice"}
        </Button>
      ) : (
        <p className="mt-3 text-xs text-muted-foreground">
          AI is switched off.
        </p>
      )}
    </Section>
  );
}

function DecisionSection({
  caseId,
  detail,
  readiness,
}: {
  caseId: string;
  detail: ReviewCaseDetail;
  readiness: ReturnType<typeof useReviewCaseReadiness>;
}) {
  const connection = useConnection();
  const saveNote = useSaveReviewNote(caseId);
  const settle = useSettleCase();
  const sync = useSyncReviewCase(caseId);

  const amount = moneyText(detail.case, "milestoneAmount");

  const readOnly = useAuthStore((state) => state.readOnly);
  const [mode, setMode] = useState<"" | "refund" | "award" | "percent" | "amount">("");
  const [percent, setPercent] = useState("50");
  const [freelancerText, setFreelancerText] = useState("0");
  const [endEscrow, setEndEscrow] = useState(false);
  const [note, setNote] = useState(detail.reviewNote ?? "");

  let freelancerAmount = mode === "award" ? amount : mode === "refund" ? "0" : freelancerText.trim();
  let rounded = false;
  let validAmount = false;
  try {
    if (mode === "percent") {
      const split = percentageSettlement(amount, percent, detail.tokenDecimals);
      freelancerAmount = split.freelancer;
      rounded = split.rounded;
    }
    validAmount = mode !== "" && decimalToBaseUnits(freelancerAmount, detail.tokenDecimals)
      <= decimalToBaseUnits(amount, detail.tokenDecimals);
  } catch { /* Invalid or inexact input keeps signing disabled. */ }

  const feePreview = validAmount
    ? previewPlatformFee(freelancerAmount, detail.tokenDecimals, detail.platformFeeBasisPoints ?? 0)
    : { fee: "0", net: "0" };

  const clientAmount = validAmount
    ? subtractMoney(amount, freelancerAmount)
    : "0";

  const isArbiterWallet =
    Boolean(connection.address) &&
    Boolean(detail.arbiterWalletAddress) &&
    connection.address?.toLowerCase() ===
      detail.arbiterWalletAddress?.toLowerCase();

  const busy = settle.isPending || saveNote.isPending || sync.isPending;
  const noteReady = note.trim().length >= 10 && note.trim().length <= 2000;

  const canSign =
    readiness.ready && validAmount && noteReady && isArbiterWallet && !readOnly && !busy;

  async function sign() {
    if (!canSign) return;
    // The reasons are saved first, so the two sides can read them as soon as
    // the settlement shows up.
    if (note.trim() !== (detail.reviewNote ?? "")) {
      await saveNote.mutateAsync(note.trim());
    }

    await settle.mutateAsync({ detail, freelancerAmount, endEscrow });

    await sync.mutateAsync();
  }

  return (
    <Section title="Your decision">
      <p className="mb-4 text-xs text-muted-foreground">Decide only this disputed milestone: {formatAmount(amount)}. The client receives the remainder. The contract deducts its fixed platform fee from the gross freelancer award.</p>
      {readOnly && <p role="note" className="mb-4 rounded-lg border p-3 text-sm">Local preview is read-only. Sign in with your administrator account to save reasons or sign a settlement.</p>}
      <div className="mb-4 flex flex-wrap gap-2" role="group" aria-label="Settlement choice">
        {([
          ["refund", "Refund client in full"], ["award", "Award freelancer in full"],
          ["percent", "Percentage split"], ["amount", "Exact amount"],
        ] as const).map(([value, label]) => <Button key={value} type="button" size="sm"
          variant={mode === value ? "default" : "outline"} aria-pressed={mode === value}
          disabled={busy || readOnly} onClick={() => setMode(value)}>{label}</Button>)}
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          {mode === "percent" && <>
            <Label htmlFor="freelancer-percent">Freelancer share of this milestone (%)</Label>
            <Input id="freelancer-percent" inputMode="decimal" value={percent} disabled={busy || readOnly}
              onChange={(event) => setPercent(event.target.value)} />
            <p className="text-xs text-muted-foreground">0–100%, with up to two decimal places. Any fraction smaller than one token base unit stays with the client.</p>
          </>}
          <Label htmlFor="freelancer-amount">Gross freelancer award (before platform fee)</Label>
          <Input id="freelancer-amount" inputMode="decimal" value={freelancerAmount}
            readOnly={mode !== "amount"} disabled={busy || readOnly || mode === ""}
            onChange={(event) => setFreelancerText(event.target.value)} />
          {detail.advice && <Button type="button" size="sm" variant="outline" disabled={busy || readOnly}
            onClick={() => { setPercent(String(detail.advice!.freelancerSharePercent)); setMode("percent"); }}>Use AI suggestion for review</Button>}
          {rounded && <p className="text-xs text-muted-foreground">The percentage award was rounded down to the smallest token unit.</p>}
        </div>
        <div className="space-y-2 text-sm">
          <p className="text-xs text-muted-foreground">Client gets back from this milestone</p>
          <p className="text-lg font-semibold">{formatAmount(clientAmount)}</p>
          <p>Platform fee: {formatAmount(feePreview.fee)}</p>
          <p>Freelancer receives: {formatAmount(feePreview.net)}</p>
          {!validAmount && <p role="alert" className="text-xs text-destructive">
            Choose a decision and enter a valid percentage or an exact token amount from 0 to {formatAmount(amount)}.
          </p>}
        </div>
      </div>

      <label className="mt-4 flex cursor-pointer items-start gap-3 text-sm">
        <input
          type="checkbox"
          className="mt-1"
          checked={endEscrow}
          disabled={busy || readOnly}
          onChange={(event) => setEndEscrow(event.target.checked)}
        />

        <span>
          End the escrow
          <span className="block text-xs leading-5 text-muted-foreground">
            Also return every later, unpaid milestone to the client, and open
            those stages for another freelancer. Use it when the freelancer
            has left or cannot continue.
          </span>
        </span>
      </label>

      <div className="mt-4 space-y-2">
        <Label htmlFor="review-note">Your reasons (both sides will read this)</Label>

        <Textarea
          id="review-note"
          rows={5}
          maxLength={2000}
          value={note}
          disabled={busy || readOnly}
          onChange={(event) => setNote(event.target.value)}
          placeholder="Say what you checked and why you chose this split. Be short and factual."
        />
      </div>

      {!noteReady && <p className="mt-2 text-xs text-muted-foreground">Write a shared reason of 10–2,000 characters before signing.</p>}

      <div className="mt-4 space-y-1 text-xs leading-5 text-muted-foreground">
        <p>
          Arbiter wallet for this escrow:{" "}
          <code>{detail.arbiterWalletAddress ?? "unknown"}</code>
        </p>

        <p>
          Connected wallet: <code>{connection.address ?? "none"}</code>
          {connection.address && !isArbiterWallet
            ? " — this is not the arbiter wallet."
            : ""}
        </p>

        <p role="status">
          {readiness.isPending && readiness.fetchStatus === "fetching"
            ? "Checking the current case and response window on the chain. Signing remains disabled."
            : settlementReadinessMessage(readiness.status)}
        </p>
      </div>

      <AlertDialog>
        <AlertDialogTrigger asChild>
          <Button type="button" className="mt-4" disabled={!canSign}>
            {busy ? <LoaderCircle className="size-4 animate-spin" /> : null}
            Sign the settlement
          </Button>
        </AlertDialogTrigger>

        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Sign this settlement?</AlertDialogTitle>

            <AlertDialogDescription asChild>
              <div className="space-y-2 text-sm">
                <p>
                  Your wallet will ask you to confirm. This cannot be undone.
                </p>

                <p>Case reference: <code className="break-all">{detail.case.referenceCode}</code></p>
                <p className="whitespace-pre-wrap">Shared reason: {note.trim()}</p>
                <ul className="list-disc space-y-1 pl-5">
                  <li>Gross freelancer award: {formatAmount(freelancerAmount)}.</li>
                  <li>Platform fee: {feePreview.fee} token units; freelancer receives {feePreview.net} token units.</li>

                  <li>Client gets back {formatAmount(clientAmount)}.</li>

                  <li>
                    {endEscrow
                      ? "The escrow ends and all later milestones go back to the client."
                      : "The escrow carries on with the next milestone."}
                  </li>
                </ul>
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>

          <AlertDialogFooter>
            <AlertDialogCancel>Not now</AlertDialogCancel>

            <AlertDialogAction onClick={() => { void sign().catch(() => { /* Mutations show the error; refresh can recover a confirmed transaction. */ }); }}>
              Sign
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Section>
  );
}

function ClosedSection({ detail }: { detail: ReviewCaseDetail }) {
  return (
    <Section title="Result">
      <p className="mb-3 text-xs text-muted-foreground">Case reference: <code className="break-all">{detail.case.referenceCode ?? "Not recorded"}</code></p>
      {detail.case.settlementTransactionHash && <p className="mb-3 text-xs text-muted-foreground">Settlement transaction: <code className="break-all">{detail.case.settlementTransactionHash}</code>{detail.case.settlementAt ? ` · ${formatDate(detail.case.settlementAt)}` : ""}</p>}
      {detail.case.status === "Settled" ? (
        <p className="text-sm leading-6">
          The freelancer received{" "}
          {formatTokenPayment(moneyText(detail.case, detail.case.freelancerNetPaid != null ? "freelancerNetPaid" : "paidToFreelancer"))} and{" "}
          {formatAmount(moneyText(detail.case, "returnedToClient"))} went back to the
          client.
          {" "}Platform fee: {formatTokenPayment(moneyText(detail.case, "platformFeePaid"))}.
          {detail.case.escrowEnded
            ? " The escrow was ended and the later milestones went back to the client."
            : ""}
        </p>
      ) : (
        <p className="text-sm text-muted-foreground">
          The request was taken back, or closed by a payment or a resignation.
        </p>
      )}

      {detail.reviewNote && (
        <p className="mt-3 whitespace-pre-wrap rounded-lg border bg-background/30 p-3 text-sm leading-6 text-muted-foreground">
          {detail.reviewNote}
        </p>
      )}
    </Section>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en-US", { dateStyle: "medium" }).format(
    new Date(value),
  );
}
