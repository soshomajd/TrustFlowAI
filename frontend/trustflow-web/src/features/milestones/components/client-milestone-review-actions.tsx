"use client";

import {
    CheckCircle2,
    LoaderCircle,
    TriangleAlert,
    XCircle,
} from "lucide-react";
import { useState } from "react";

import { Button } from
    "@/components/ui/button";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from "@/components/ui/dialog";
import { useApproveMilestone } from
    "@/features/milestones/hooks/use-approve-milestone";
import { useRejectMilestone } from
    "@/features/milestones/hooks/use-reject-milestone";
import type {
    MilestoneStatus,
} from "@/features/milestones/types/milestone";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";

type ClientMilestoneReviewActionsProps = {
    projectId: string;
    milestoneId: string;
    status: MilestoneStatus;
};

export function ClientMilestoneReviewActions({
    projectId,
    milestoneId,
    status,
}: ClientMilestoneReviewActionsProps) {
    if (status !== "Submitted") {
        return null;
    }

    return (
        <div className="mt-5 flex flex-col gap-3 border-t pt-5 sm:flex-row sm:justify-end">
            <RejectMilestoneDialog
                projectId={projectId}
                milestoneId={milestoneId}
            />

            <ApproveMilestoneDialog
                projectId={projectId}
                milestoneId={milestoneId}
            />
        </div>
    );
}

function ApproveMilestoneDialog({
    projectId,
    milestoneId,
}: {
    projectId: string;
    milestoneId: string;
}) {
    const [open, setOpen] =
        useState(false);

    const approveMutation =
        useApproveMilestone(
            projectId,
            milestoneId,
        );

    const errorMessage =
        approveMutation.isError
            ? getApiErrorMessage(
                approveMutation.error,
                "Milestone could not be approved.",
            )
            : null;

    function handleApprove() {
        approveMutation.mutate(
            undefined,
            {
                onSuccess: () => {
                    setOpen(false);
                },
            },
        );
    }

    return (
        <Dialog
            open={open}
            onOpenChange={(nextOpen) => {
                if (
                    approveMutation.isPending
                ) {
                    return;
                }

                if (nextOpen) {
                    approveMutation.reset();
                }

                setOpen(nextOpen);
            }}
        >
            <DialogTrigger asChild>
                <Button type="button">
                    <CheckCircle2 className="size-4" />
                    Approve milestone
                </Button>
            </DialogTrigger>

            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle>
                        Approve this milestone?
                    </DialogTitle>

                    <DialogDescription>
                        The milestone will be marked
                        as approved. The freelancer
                        can then start the next
                        milestone.
                    </DialogDescription>
                </DialogHeader>

                {errorMessage && (
                    <div
                        role="alert"
                        className="flex gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4"
                    >
                        <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                        <p className="text-sm text-destructive">
                            {errorMessage}
                        </p>
                    </div>
                )}

                <DialogFooter>
                    <Button
                        type="button"
                        variant="outline"
                        disabled={
                            approveMutation.isPending
                        }
                        onClick={() => {
                            setOpen(false);
                        }}
                    >
                        Cancel
                    </Button>

                    <Button
                        type="button"
                        disabled={
                            approveMutation.isPending
                        }
                        onClick={handleApprove}
                    >
                        {approveMutation.isPending ? (
                            <>
                                <LoaderCircle className="size-4 animate-spin" />
                                Approving
                            </>
                        ) : (
                            <>
                                <CheckCircle2 className="size-4" />
                                Approve
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

function RejectMilestoneDialog({
    projectId,
    milestoneId,
}: {
    projectId: string;
    milestoneId: string;
}) {
    const [open, setOpen] =
        useState(false);

    const rejectMutation =
        useRejectMilestone(
            projectId,
            milestoneId,
        );

    const errorMessage =
        rejectMutation.isError
            ? getApiErrorMessage(
                rejectMutation.error,
                "Milestone could not be rejected.",
            )
            : null;

    function handleReject() {
        rejectMutation.mutate(
            undefined,
            {
                onSuccess: () => {
                    setOpen(false);
                },
            },
        );
    }

    return (
        <Dialog
            open={open}
            onOpenChange={(nextOpen) => {
                if (
                    rejectMutation.isPending
                ) {
                    return;
                }

                if (nextOpen) {
                    rejectMutation.reset();
                }

                setOpen(nextOpen);
            }}
        >
            <DialogTrigger asChild>
                <Button
                    type="button"
                    variant="outline"
                >
                    <XCircle className="size-4" />
                    Reject
                </Button>
            </DialogTrigger>

            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle>
                        Reject this milestone?
                    </DialogTitle>

                    <DialogDescription>
                        The milestone will be
                        returned to the freelancer
                        with Rejected status. The
                        freelancer can restart and
                        submit it again.
                    </DialogDescription>
                </DialogHeader>

                {errorMessage && (
                    <div
                        role="alert"
                        className="flex gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4"
                    >
                        <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                        <p className="text-sm text-destructive">
                            {errorMessage}
                        </p>
                    </div>
                )}

                <DialogFooter>
                    <Button
                        type="button"
                        variant="outline"
                        disabled={
                            rejectMutation.isPending
                        }
                        onClick={() => {
                            setOpen(false);
                        }}
                    >
                        Cancel
                    </Button>

                    <Button
                        type="button"
                        variant="destructive"
                        disabled={
                            rejectMutation.isPending
                        }
                        onClick={handleReject}
                    >
                        {rejectMutation.isPending ? (
                            <>
                                <LoaderCircle className="size-4 animate-spin" />
                                Rejecting
                            </>
                        ) : (
                            <>
                                <XCircle className="size-4" />
                                Reject milestone
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}