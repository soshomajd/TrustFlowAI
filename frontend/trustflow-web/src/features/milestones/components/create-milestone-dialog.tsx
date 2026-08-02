"use client";

import { Plus } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from "@/components/ui/dialog";
import { CreateMilestoneForm } from "@/features/milestones/components/create-milestone-form";
import type { ProjectStatus } from "@/features/projects/types/project";

type CreateMilestoneDialogProps = {
    projectId: string;
    projectDeadline: string;
    projectStatus: ProjectStatus;

    remainingBudget: number;
    existingSequenceNumbers: number[];
};

export function CreateMilestoneDialog({
    projectId,
    projectDeadline,
    projectStatus,
    remainingBudget,
    existingSequenceNumbers,
}: CreateMilestoneDialogProps) {
    const [open, setOpen] =
        useState(false);

    const isProjectOpen =
        projectStatus === "Open";

    const hasRemainingBudget =
        remainingBudget >= 0.01;

    const canCreate =
        isProjectOpen &&
        hasRemainingBudget;

    const disabledReason =
        !isProjectOpen
            ? "Milestones can only be created while the project is open."
            : !hasRemainingBudget
                ? "The full project budget has already been allocated."
                : undefined;

    return (
        <Dialog
            open={open}
            onOpenChange={setOpen}
        >
            <DialogTrigger asChild>
                <Button
                    type="button"
                    size="sm"
                    disabled={!canCreate}
                    title={disabledReason}
                >
                    <Plus className="size-4" />
                    Add milestone
                </Button>
            </DialogTrigger>

            <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
                <DialogHeader>
                    <DialogTitle>
                        Create milestone
                    </DialogTitle>

                    <DialogDescription>
                        Define a delivery stage,
                        its budget, sequence and
                        deadline.
                    </DialogDescription>
                </DialogHeader>

                <CreateMilestoneForm
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
                    onCreated={() => {
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