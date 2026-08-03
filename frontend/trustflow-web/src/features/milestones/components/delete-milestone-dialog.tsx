"use client";

import { LoaderCircle, Trash2, TriangleAlert, } from "lucide-react";
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
import { useDeleteMilestone } from "@/features/milestones/hooks/use-delete-milestone";
import type { ProjectMilestone } from "@/features/milestones/types/milestone";

type DeleteMilestoneDialogProps = {
    projectId: string;
    milestone: ProjectMilestone;
};

export function DeleteMilestoneDialog({
    projectId,
    milestone,
}: DeleteMilestoneDialogProps) {
    const [open, setOpen] = useState(false);

    const deleteMutation = useDeleteMilestone(projectId);

    const isDeleting = deleteMutation.isPending;

    function handleOpenChange(nextOpen: boolean,) {
        if (isDeleting) {
            return;
        }

        setOpen(nextOpen);
    }

    function handleDelete() {
        deleteMutation.mutate(
            milestone.id,
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
            onOpenChange={
                handleOpenChange
            }
        >
            <DialogTrigger asChild>
                <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                    aria-label={`Delete ${milestone.title}`}
                >
                    <Trash2 className="size-4" />
                </Button>
            </DialogTrigger>

            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <div className="mb-2 flex size-11 items-center justify-center rounded-full bg-destructive/10">
                        <TriangleAlert className="size-5 text-destructive" />
                    </div>

                    <DialogTitle>
                        Delete milestone?
                    </DialogTitle>

                    <DialogDescription className="leading-6">
                        You are about to delete{" "}
                        <span className="font-medium text-foreground">
                            {milestone.title}
                        </span>
                        . This action cannot be
                        undone.
                    </DialogDescription>
                </DialogHeader>

                <div className="rounded-xl border bg-muted/30 p-4 text-sm">
                    <div className="flex items-center justify-between gap-4">
                        <span className="text-muted-foreground">
                            Sequence
                        </span>

                        <span className="font-medium">
                            {milestone.sequenceNumber}
                        </span>
                    </div>

                    <div className="mt-3 flex items-center justify-between gap-4">
                        <span className="text-muted-foreground">
                            Amount
                        </span>

                        <span className="font-medium">
                            {formatAmount(
                                milestone.amount,
                            )}
                        </span>
                    </div>
                </div>

                <DialogFooter className="gap-2 sm:gap-0">
                    <Button
                        type="button"
                        variant="outline"
                        disabled={isDeleting}
                        onClick={() => {
                            setOpen(false);
                        }}
                    >
                        Cancel
                    </Button>

                    <Button
                        type="button"
                        variant="destructive"
                        disabled={isDeleting}
                        onClick={handleDelete}
                    >
                        {isDeleting ? (
                            <>
                                <LoaderCircle className="size-4 animate-spin" />
                                Deleting
                            </>
                        ) : (
                            <>
                                <Trash2 className="size-4" />
                                Delete milestone
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

function formatAmount(
    value: number,
) {
    return new Intl.NumberFormat(
        "en-US",
        {
            minimumFractionDigits: 0,
            maximumFractionDigits: 2,
        },
    ).format(value);
}