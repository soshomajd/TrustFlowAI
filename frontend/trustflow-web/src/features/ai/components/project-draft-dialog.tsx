"use client";

import { LoaderCircle, Sparkles } from "lucide-react";
import { useState } from "react";

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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AiDemoNotice } from "@/features/ai/components/ai-demo-notice";
import { useProjectDraft } from "@/features/ai/hooks/use-ai-assistants";
import type { ProjectDraft } from "@/features/ai/types/ai";

type ProjectDraftDialogProps = {
  disabled?: boolean;
  onApply: (draft: ProjectDraft) => void;
};

export function ProjectDraftDialog({
  disabled,
  onApply,
}: ProjectDraftDialogProps) {
  const [open, setOpen] = useState(false);
  const [idea, setIdea] = useState("");
  const [budget, setBudget] = useState("");
  const [days, setDays] = useState("");
  const [problem, setProblem] = useState<string | null>(null);
  const mutation = useProjectDraft();

  const draft = mutation.data;

  function reset() {
    mutation.reset();
    setProblem(null);
  }

  function makeDraft() {
    setProblem(null);

    const text = idea.trim();

    if (text.length < 10) {
      setProblem("Describe your idea in at least a few words.");
      return;
    }

    const budgetValue = budget.trim() === "" ? undefined : Number(budget);
    const daysValue = days.trim() === "" ? undefined : Number(days);

    if (
      budgetValue !== undefined &&
      (!Number.isFinite(budgetValue) ||
        budgetValue < 0.01 ||
        !/^\d+(\.\d{1,2})?$/.test(budget.trim()))
    ) {
      setProblem("Budget must be a number with at most 2 decimal places.");
      return;
    }

    if (
      daysValue !== undefined &&
      (!Number.isInteger(daysValue) || daysValue < 1 || daysValue > 730)
    ) {
      setProblem("Days must be a whole number from 1 to 730.");
      return;
    }

    mutation.mutate({
      idea: text,
      budgetHint: budgetValue,
      deadlineDays: daysValue,
    });
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);

        if (!next) {
          reset();
        }
      }}
    >
      <DialogTrigger asChild>
        <Button type="button" variant="outline" disabled={disabled}>
          <Sparkles className="size-4" />
          Draft with AI
        </Button>
      </DialogTrigger>

      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Draft your project with AI</DialogTitle>

          <DialogDescription>
            Write your idea in your own words. The AI suggests a title, a
            description, a price and milestones. You can change everything
            before you create the project.
          </DialogDescription>
        </DialogHeader>

        {!draft ? (
          <div className="space-y-4">
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label htmlFor="ai-idea">Your idea</Label>

                <span className="text-xs text-muted-foreground">
                  {idea.length}/3000
                </span>
              </div>

              <Textarea
                id="ai-idea"
                rows={6}
                maxLength={3000}
                value={idea}
                disabled={mutation.isPending}
                onChange={(event) => setIdea(event.target.value)}
                placeholder="Example: I own a bakery and I need a simple website with our menu, opening hours and a contact form."
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="ai-budget">Your budget (optional)</Label>

                <Input
                  id="ai-budget"
                  inputMode="decimal"
                  value={budget}
                  disabled={mutation.isPending}
                  onChange={(event) => setBudget(event.target.value)}
                  placeholder="Leave empty to get a suggestion"
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="ai-days">Finish within (optional)</Label>

                <Input
                  id="ai-days"
                  inputMode="numeric"
                  value={days}
                  disabled={mutation.isPending}
                  onChange={(event) => setDays(event.target.value)}
                  placeholder="Number of days"
                />
              </div>
            </div>

            {problem && (
              <p role="alert" className="text-sm text-destructive">
                {problem}
              </p>
            )}

            <p className="text-xs leading-5 text-muted-foreground">
              Do not put passwords, private keys or other secrets in your idea.
            </p>
          </div>
        ) : (
          <div className="space-y-5">
            <AiDemoNotice isDemo={draft.isDemo} />

            <section>
              <h3 className="text-sm font-semibold">{draft.title}</h3>

              <p className="mt-2 max-h-40 overflow-y-auto whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                {draft.description}
              </p>
            </section>

            <section className="rounded-xl border bg-background/35 p-4">
              <p className="text-sm font-semibold">
                Suggested budget: {draft.budget.toFixed(2)}
              </p>

              {draft.priceRationale && (
                <p className="mt-1 text-sm leading-6 text-muted-foreground">
                  {draft.priceRationale}
                </p>
              )}
            </section>

            <section>
              <h3 className="text-sm font-semibold">Milestones</h3>

              <ol className="mt-3 space-y-2">
                {draft.milestones.map((milestone) => (
                  <li
                    key={milestone.sequenceNumber}
                    className="rounded-xl border bg-background/35 p-3"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <p className="text-sm font-medium">
                        {milestone.sequenceNumber}. {milestone.title}
                      </p>

                      <span className="shrink-0 text-sm text-electric">
                        {milestone.amount.toFixed(2)}
                      </span>
                    </div>

                    <p className="mt-1 text-xs leading-5 text-muted-foreground">
                      {milestone.description}
                    </p>

                    <p className="mt-1 text-xs text-muted-foreground">
                      Due about {milestone.dueInDays} days after you start.
                    </p>
                  </li>
                ))}
              </ol>
            </section>

            {draft.assumptions.length > 0 && (
              <section>
                <h3 className="text-sm font-semibold">What the AI assumed</h3>

                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                  {draft.assumptions.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </section>
            )}

            {draft.questionsForClient.length > 0 && (
              <section>
                <h3 className="text-sm font-semibold">Think about these</h3>

                <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                  {draft.questionsForClient.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </section>
            )}

            <p className="text-xs leading-5 text-muted-foreground">
              Using this draft fills in the form and plans the milestones. It
              replaces what you have typed so far. Nothing is created until you
              press Create project.
            </p>
          </div>
        )}

        <DialogFooter>
          {!draft ? (
            <Button
              type="button"
              disabled={mutation.isPending}
              onClick={makeDraft}
            >
              {mutation.isPending ? (
                <>
                  <LoaderCircle className="size-4 animate-spin" />
                  Drafting
                </>
              ) : (
                <>
                  <Sparkles className="size-4" />
                  Make a draft
                </>
              )}
            </Button>
          ) : (
            <>
              <Button type="button" variant="outline" onClick={reset}>
                Try again
              </Button>

              <Button
                type="button"
                onClick={() => {
                  onApply(draft);
                  setOpen(false);
                  reset();
                }}
              >
                Use this draft
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
