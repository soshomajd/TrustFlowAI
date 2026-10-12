"use client";

import {
  Check,
  Copy,
  Download,
  FileText,
  LoaderCircle,
  Sparkles,
} from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AiDemoNotice } from "@/features/ai/components/ai-demo-notice";
import { useAiStatus } from "@/features/ai/hooks/use-ai-status";
import {
  useProfileCoach,
  useResumeDraft,
} from "@/features/ai/hooks/use-ai-assistants";
import type { ProfileCoaching } from "@/features/ai/types/ai";

type FreelancerAiCoachProps = {
  onApplyHeadline: (value: string) => void;
  onApplyAbout: (value: string) => void;
  onApplySkills: (values: string[]) => void;
  onAddPortfolioItem: (item: { title: string; description: string }) => void;

  // How many more work samples the profile can hold.
  portfolioSlotsLeft: number;
};

// Suggestions for the profile editor. Nothing here saves anything: every
// "Use" button only fills a field in the form, and the freelancer still
// reads it, edits it and presses Save.
export function FreelancerAiCoach({
  onApplyHeadline,
  onApplyAbout,
  onApplySkills,
  onAddPortfolioItem,
  portfolioSlotsLeft,
}: FreelancerAiCoachProps) {
  const status = useAiStatus();
  const coach = useProfileCoach();
  const resume = useResumeDraft();

  const [coachOpen, setCoachOpen] = useState(false);
  const [resumeOpen, setResumeOpen] = useState(false);
  const [extraFacts, setExtraFacts] = useState("");

  if (!status.data?.enabled) {
    return null;
  }

  function openCoach() {
    setCoachOpen(true);

    if (!coach.data && !coach.isPending) {
      coach.mutate();
    }
  }

  return (
    <section className="rounded-2xl border border-primary/20 bg-primary/5 p-5">
      <div className="flex items-center gap-2">
        <Sparkles className="size-4 text-electric" />

        <h2 className="text-sm font-semibold">AI help</h2>
      </div>

      <p className="mt-2 text-sm leading-6 text-muted-foreground">
        Get a review of your profile with suggested wording, or a resume draft
        built from your profile and your finished TrustFlow work. The AI only
        suggests. You decide what to keep.
      </p>

      <div className="mt-4 flex flex-col gap-2">
        <Button type="button" variant="outline" onClick={openCoach}>
          <Sparkles className="size-4" />
          Review my profile
        </Button>

        <Button
          type="button"
          variant="outline"
          onClick={() => setResumeOpen(true)}
        >
          <FileText className="size-4" />
          Draft my resume
        </Button>
      </div>

      <Dialog open={coachOpen} onOpenChange={setCoachOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Your profile, reviewed</DialogTitle>

            <DialogDescription>
              Suggestions use only what is in your profile and your finished
              work. Check that they are true before you keep them.
            </DialogDescription>
          </DialogHeader>

          {coach.isPending && (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <LoaderCircle className="size-4 animate-spin" />
              Reading your profile
            </p>
          )}

          {coach.isError && (
            <Button type="button" variant="outline" onClick={() => coach.mutate()}>
              Try again
            </Button>
          )}

          {coach.data && (
            <CoachResult
              coaching={coach.data}
              portfolioSlotsLeft={portfolioSlotsLeft}
              onApplyHeadline={onApplyHeadline}
              onApplyAbout={onApplyAbout}
              onApplySkills={onApplySkills}
              onAddPortfolioItem={onAddPortfolioItem}
            />
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={resumeOpen} onOpenChange={setResumeOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Draft my resume</DialogTitle>

            <DialogDescription>
              The AI uses your profile, your finished TrustFlow work and your
              uploaded resume. Add anything else you want on it. Where facts
              are missing it leaves a gap instead of making something up.
            </DialogDescription>
          </DialogHeader>

          {!resume.data && (
            <div className="space-y-3">
              <div className="space-y-2">
                <Label htmlFor="resume-facts">
                  Anything else to include (optional)
                </Label>

                <Textarea
                  id="resume-facts"
                  rows={5}
                  maxLength={3000}
                  value={extraFacts}
                  disabled={resume.isPending}
                  onChange={(event) => setExtraFacts(event.target.value)}
                  placeholder="Past jobs, education, courses, anything true that helps."
                />

                <p className="text-xs text-muted-foreground">
                  {extraFacts.length}/3000. Do not include secrets.
                </p>
              </div>

              <Button
                type="button"
                disabled={resume.isPending}
                onClick={() =>
                  resume.mutate({ extraFacts: extraFacts.trim() || undefined })
                }
              >
                {resume.isPending ? (
                  <>
                    <LoaderCircle className="size-4 animate-spin" />
                    Writing
                  </>
                ) : (
                  "Write the draft"
                )}
              </Button>
            </div>
          )}

          {resume.data && (
            <div className="space-y-3">
              <AiDemoNotice isDemo={resume.data.isDemo} />

              <Textarea
                readOnly
                rows={16}
                value={resume.data.resumeMarkdown}
                aria-label="Resume draft"
                className="font-mono text-xs"
              />

              {resume.data.notes.length > 0 && (
                <div className="rounded-lg border border-dashed p-3">
                  <p className="text-xs font-semibold">Fill these in</p>

                  <ul className="mt-1 list-disc space-y-1 pl-5 text-xs leading-5 text-muted-foreground">
                    {resume.data.notes.map((note) => (
                      <li key={note}>{note}</li>
                    ))}
                  </ul>
                </div>
              )}

              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={async () => {
                    try {
                      await navigator.clipboard.writeText(
                        resume.data?.resumeMarkdown ?? "",
                      );

                      toast.success("Resume copied.");
                    } catch {
                      toast.error("Could not copy. Select the text instead.");
                    }
                  }}
                >
                  <Copy className="size-3.5" />
                  Copy
                </Button>

                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    const blob = new Blob(
                      [resume.data?.resumeMarkdown ?? ""],
                      { type: "text/markdown" },
                    );

                    const url = URL.createObjectURL(blob);
                    const anchor = document.createElement("a");

                    anchor.href = url;
                    anchor.download = "resume.md";
                    anchor.click();

                    URL.revokeObjectURL(url);
                  }}
                >
                  <Download className="size-3.5" />
                  Download
                </Button>

                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => resume.reset()}
                >
                  Write again
                </Button>
              </div>

              <p className="text-xs leading-5 text-muted-foreground">
                This is a draft in Markdown. Read it, fix it and turn it into a
                PDF yourself, then upload that PDF as your resume. The AI does
                not upload anything for you.
              </p>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </section>
  );
}

function CoachResult({
  coaching,
  portfolioSlotsLeft,
  onApplyHeadline,
  onApplyAbout,
  onApplySkills,
  onAddPortfolioItem,
}: { coaching: ProfileCoaching } & Omit<FreelancerAiCoachProps, never>) {
  const [used, setUsed] = useState<Set<string>>(new Set());

  function markUsed(key: string) {
    setUsed((current) => new Set(current).add(key));

    toast.success("Added to your form. Press Save changes to keep it.");
  }

  return (
    <div className="space-y-5">
      <AiDemoNotice isDemo={coaching.isDemo} />

      <div>
        <div className="flex items-center justify-between text-sm">
          <span className="font-medium">How complete your profile is</span>

          <span className="text-electric">{coaching.completenessScore}%</span>
        </div>

        <div
          className="mt-2 h-1.5 overflow-hidden rounded-full bg-muted"
          role="progressbar"
          aria-valuenow={coaching.completenessScore}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label="Profile completeness"
        >
          <div
            className="h-full rounded-full bg-primary"
            style={{ width: `${coaching.completenessScore}%` }}
          />
        </div>

        <p className="mt-3 text-sm leading-6 text-muted-foreground">
          {coaching.summary}
        </p>
      </div>

      {coaching.missing.length > 0 && (
        <section>
          <h3 className="text-sm font-semibold">What to add</h3>

          <ul className="mt-2 space-y-2">
            {coaching.missing.map((gap) => (
              <li
                key={`${gap.field}-${gap.advice}`}
                className="rounded-lg border bg-background/35 p-3"
              >
                <p className="text-sm font-medium">{gap.field}</p>

                <p className="mt-1 text-xs leading-5 text-muted-foreground">
                  {gap.advice}
                </p>
              </li>
            ))}
          </ul>
        </section>
      )}

      {coaching.suggestedHeadline && (
        <Suggestion
          title="Professional title"
          text={coaching.suggestedHeadline}
          done={used.has("headline")}
          onUse={() => {
            onApplyHeadline(coaching.suggestedHeadline);
            markUsed("headline");
          }}
        />
      )}

      {coaching.suggestedAbout && (
        <Suggestion
          title="Short introduction"
          text={coaching.suggestedAbout}
          done={used.has("about")}
          onUse={() => {
            onApplyAbout(coaching.suggestedAbout);
            markUsed("about");
          }}
        />
      )}

      {coaching.suggestedSkills.length > 0 && (
        <Suggestion
          title="Skills"
          text={coaching.suggestedSkills.join(", ")}
          done={used.has("skills")}
          onUse={() => {
            onApplySkills(coaching.suggestedSkills);
            markUsed("skills");
          }}
        />
      )}

      {coaching.suggestedPortfolioItems.map((item, index) => (
        <Suggestion
          key={`${item.title}-${index}`}
          title={`Work sample: ${item.title}`}
          text={item.description}
          done={used.has(`work-${index}`)}
          disabled={portfolioSlotsLeft <= 0}
          disabledReason="Your profile already has 3 work samples."
          useLabel="Add as work sample"
          onUse={() => {
            onAddPortfolioItem(item);
            markUsed(`work-${index}`);
          }}
        />
      ))}

      <p className="text-xs leading-5 text-muted-foreground">
        This is an opinion from an AI and it can be wrong. Keep only what is
        true about you.
      </p>
    </div>
  );
}

function Suggestion({
  title,
  text,
  done,
  disabled,
  disabledReason,
  useLabel = "Use this",
  onUse,
}: {
  title: string;
  text: string;
  done: boolean;
  disabled?: boolean;
  disabledReason?: string;
  useLabel?: string;
  onUse: () => void;
}) {
  return (
    <section className="rounded-lg border bg-background/35 p-3">
      <div className="flex items-start justify-between gap-3">
        <h3 className="text-sm font-semibold">{title}</h3>

        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={done || disabled}
          title={disabled ? disabledReason : undefined}
          onClick={onUse}
        >
          {done ? (
            <>
              <Check className="size-3.5" />
              Added
            </>
          ) : (
            useLabel
          )}
        </Button>
      </div>

      <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
        {text}
      </p>
    </section>
  );
}
