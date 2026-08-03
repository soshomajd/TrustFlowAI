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
import { SubmitProposalForm } from
    "@/features/proposals/components/submit-proposal-form";

type SubmitProposalDialogProps = {
    projectId: string;

    projectTitle?: string;
    projectBudget?: number;

    disabled?: boolean;
    disabledReason?: string;
};

export function SubmitProposalDialog({
    projectId,
    projectTitle,
    projectBudget,
    disabled = false,
    disabledReason,
}: SubmitProposalDialogProps) {
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
                    disabled={disabled}
                    title={disabledReason}
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
                        Introduce yourself, enter
                        your bid and estimate how
                        long the project will take.
                    </DialogDescription>
                </DialogHeader>

                <SubmitProposalForm
                    projectId={projectId}
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
    );
}