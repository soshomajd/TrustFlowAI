"use client";

import {
  CheckCircle2,
  CircleHelp,
  Copy,
  LoaderCircle,
  MinusCircle,
  Sparkles,
  XCircle,
} from "lucide-react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { AiDemoNotice } from "@/features/ai/components/ai-demo-notice";
import { useAiStatus } from "@/features/ai/hooks/use-ai-status";
import {
  useMilestoneAiReview,
  useRequestMilestoneAiReview,
} from "@/features/ai/hooks/use-milestone-ai-review";
import type {
  CriterionStatus,
  ReviewRecommendation,
} from "@/features/ai/types/ai";

const recommendationText: Record<
  ReviewRecommendation,
  { label: string; tone: string }
> = {
  Approve: {
    label: "The AI thinks this meets the request",
    tone: "border-emerald-500/30 bg-emerald-500/10 text-emerald-300",
  },
  RequestChanges: {
    label: "The AI thinks changes are needed",
    tone: "border-amber-500/30 bg-amber-500/10 text-amber-200",
  },
  InsufficientEvidence: {
    label: "The AI cannot tell from what it could see",
    tone: "border-slate-500/30 bg-slate-500/10 text-slate-300",
  },
};

const criterionText: Record<
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

type MilestoneAiReviewPanelProps = {
  projectId: string;
  milestoneId: string;
};

// The client's second pair of eyes on a delivery. It reads the work against
// the milestone they wrote. It never approves or pays: that stays a button
// the client presses themselves.
export function MilestoneAiReviewPanel({
  projectId,
  milestoneId,
}: MilestoneAiReviewPanelProps) {
  const status = useAiStatus();
  const query = useMilestoneAiReview(
    projectId,
    milestoneId,
    status.data?.enabled === true,
  );
  const request = useRequestMilestoneAiReview(projectId, milestoneId);

  if (!status.data?.enabled) {
    return null;
  }

  const review = query.data ?? null;

  async function copyFeedback(text: string) {
    try {
      await navigator.clipboard.writeText(text);

      toast.success("Message copied. Paste it into Request changes.");
    } catch {
      toast.error("Could not copy. Select the text and copy it by hand.");
    }
  }

  return (
    <section className="mt-5 rounded-xl border border-primary/20 bg-primary/5 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Sparkles className="size-4 text-electric" />

          <h4 className="text-sm font-semibold">AI check of this delivery</h4>
        </div>

        {!review && !query.isLoading && (
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={request.isPending}
            onClick={() => request.mutate()}
          >
            {request.isPending ? (
              <>
                <LoaderCircle className="size-3.5 animate-spin" />
                Checking
              </>
            ) : (
              "Check with AI"
            )}
          </Button>
        )}
      </div>

      {!review && (
        <p className="mt-2 text-xs leading-5 text-muted-foreground">
          The AI reads the delivery against the milestone you wrote and tells
          you what is met and what is not. It can only read uploaded PDFs and
          images and the delivery note. It does not open links. You decide
          whether to approve.
        </p>
      )}

      {review && (
        <div className="mt-4 space-y-4">
          <AiDemoNotice isDemo={review.isDemo} />

          <p
            className={`rounded-lg border px-3 py-2 text-sm font-medium ${recommendationText[review.recommendation].tone}`}
          >
            {recommendationText[review.recommendation].label}
            <span className="ml-2 text-xs font-normal opacity-80">
              Confidence {Math.round(review.confidence * 100)}%
            </span>
          </p>

          <p className="text-sm leading-6 text-muted-foreground">
            {review.summary}
          </p>

          {review.criteria.length > 0 && (
            <ul className="space-y-2">
              {review.criteria.map((criterion) => {
                const meta = criterionText[criterion.status];
                const Icon = meta.icon;

                return (
                  <li
                    key={criterion.requirement}
                    className="rounded-lg border bg-background/35 p-3"
                  >
                    <p className="flex items-start gap-2 text-sm font-medium">
                      <Icon
                        className={`mt-0.5 size-4 shrink-0 ${meta.tone}`}
                        aria-hidden
                      />

                      <span>
                        {criterion.requirement}
                        <span className={`ml-2 text-xs ${meta.tone}`}>
                          {meta.label}
                        </span>
                      </span>
                    </p>

                    {criterion.evidence && (
                      <p className="mt-1 pl-6 text-xs leading-5 text-muted-foreground">
                        {criterion.evidence}
                      </p>
                    )}
                  </li>
                );
              })}
            </ul>
          )}

          {review.limitations.length > 0 && (
            <div className="rounded-lg border border-dashed p-3">
              <p className="text-xs font-semibold">
                What the AI could not check
              </p>

              <ul className="mt-1 list-disc space-y-1 pl-5 text-xs leading-5 text-muted-foreground">
                {review.limitations.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          )}

          {review.suggestedFeedback && (
            <div className="rounded-lg border bg-background/35 p-3">
              <p className="text-xs font-semibold">
                Message you could send to the freelancer
              </p>

              <p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                {review.suggestedFeedback}
              </p>

              <Button
                type="button"
                size="sm"
                variant="outline"
                className="mt-2"
                onClick={() => void copyFeedback(review.suggestedFeedback)}
              >
                <Copy className="size-3.5" />
                Copy message
              </Button>
            </div>
          )}

          <p className="text-xs leading-5 text-muted-foreground">
            This is an opinion from an AI and it can be wrong. One check is
            made for each delivery. You decide whether to approve it.
          </p>
        </div>
      )}
    </section>
  );
}
