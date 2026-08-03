"use client";

import { Pencil } from
    "lucide-react";
import { useState } from
    "react";

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
import { EditMilestoneForm } from
    "@/features/milestones/components/edit-milestone-form";
import type { ProjectMilestone } from
    "@/features/milestones/types/milestone";

type EditMilestoneDialogProps = {
    projectId: string;
    projectDeadline: string;
    remainingBudget: number;

    existingSequenceNumbers: number[];
    milestone: ProjectMilestone;
};

export function EditMilestoneDialog({
    projectId,
    projectDeadline,
    remainingBudget,
    existingSequenceNumbers,
    milestone,
}: EditMilestoneDialogProps) {
    const [open, setOpen] =
        useState(false);

    return (
        <Dialog
            open={open}
            onOpenChange={setOpen}
        >
            <DialogTrigger asChild>
                <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={`Edit ${milestone.title}`}
                >
                    <Pencil className="size-4" />
                </Button>
            </DialogTrigger>

            <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
                <DialogHeader>
                    <DialogTitle>
                        Edit milestone
                    </DialogTitle>

                    <DialogDescription>
                        Update the milestone
                        details, budget, sequence
                        or deadline.
                    </DialogDescription>
                </DialogHeader>

                <EditMilestoneForm
                    projectId={projectId}
                    projectDeadline={
                        projectDeadline
                    }
                    remainingBudget={
                        remainingBudget
                    }
                    existingSequenceNumbers={
                        existingSequenceNumbers
                    }
                    milestone={milestone}
                    onUpdated={() => {
                        setOpen(false);
                    }}
                    onCancel={() => {
                        setOpen(false);
                    }}
                />
            </DialogContent>
        </Dialog>
    );
}