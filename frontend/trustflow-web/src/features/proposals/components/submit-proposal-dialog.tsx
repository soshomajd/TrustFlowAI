"use client";

import {
    FilePenLine,
    Send,
} from "lucide-react";
import { useState } from "react";

import { Button } from
    "@/components/ui/button";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from
    "@/components/ui/dialog";
import type { PublicMarketplaceMilestone } from "@/features/projects/types/marketplace-project-details";
import { SubmitProposalForm } from
    "@/features/proposals/components/submit-proposal-form";
import { useProposalEligibility } from "@/features/proposals/hooks/use-proposal-eligibility";

type SubmitProposalDialogProps = {
    projectId: string;

    projectTitle?: string;
    projectBudget?: number | string;

    milestones: PublicMarketplaceMilestone[];

    disabled?: boolean;
    disabledReason?: string;
};

export function SubmitProposalDialog({
    projectId,
    projectTitle,
    projectBudget,
    milestones,
    disabled = false,
    disabledReason,
}: SubmitProposalDialogProps) {
    const [open, setOpen] =
        useState(false);
    const eligibility = useProposalEligibility(projectId);
    const isDisabled = disabled || eligibility.isFetching || eligibility.isError || eligibility.data?.canSubmit !== true;
    const reason = disabled ? disabledReason
        : eligibility.isFetching ? "Checking whether you can submit a proposal…"
        : eligibility.isError ? "Could not check whether you can apply. Try again."
        : eligibility.data?.message ?? (eligibility.data ? undefined : "Checking whether you can submit a proposal…");

    return (
        <div className="space-y-2">
            <Dialog
                open={open}
                onOpenChange={(nextOpen) => {
                    setOpen(nextOpen);
                    if (nextOpen) void eligibility.refetch();
                }}
            >
                <DialogTrigger asChild>
                    <Button
                        type="button"
                        disabled={isDisabled}
                        title={reason}
                        aria-describedby={reason ? `proposal-eligibility-${projectId}` : undefined}
                    >
                        <Send className="size-4" />
                        Submit proposal
                    </Button>
                </DialogTrigger>

                <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
                    <DialogHeader>
                        <div className="mb-1 flex size-10 items-center justify-center rounded-xl bg-primary/10">
                            <FilePenLine className="size-5 text-primary" />
                        </div>

                        <DialogTitle>
                            Submit proposal
                        </DialogTitle>

                        <DialogDescription>
                            Introduce yourself, pick
                            the stages you will deliver
                            and estimate how long they
                            will take.
                        </DialogDescription>
                    </DialogHeader>

                    <SubmitProposalForm
                        projectId={projectId}
                        disabled={isDisabled}
                        disabledReason={reason}
                        milestones={milestones}
                        projectTitle={
                            projectTitle
                        }
                        projectBudget={
                            projectBudget
                        }
                        onSubmitted={() => {
                            setOpen(false);
                        }}
                        onCancel={() => {
                            setOpen(false);
                        }}
                    />
                </DialogContent>
            </Dialog>
            {reason && (
                <p id={`proposal-eligibility-${projectId}`} role="status" className="text-sm text-muted-foreground">
                    {reason}
                </p>
            )}
            {eligibility.isError && (
                <Button type="button" variant="outline" size="sm" disabled={eligibility.isFetching} onClick={() => void eligibility.refetch()}>
                    Try again
                </Button>
            )}
        </div>
    );
}
