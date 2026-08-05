"use client";

import {
    LoaderCircle,
    RotateCcw,
    TriangleAlert,
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
import { useWithdrawProposal } from
    "@/features/proposals/hooks/use-withdraw-proposal";

type WithdrawProposalDialogProps = {
    proposalId: string;
    projectTitle: string;
};

export function WithdrawProposalDialog({
    proposalId,
    projectTitle,
}: WithdrawProposalDialogProps) {
    const [open, setOpen] =
        useState(false);

    const withdrawMutation =
        useWithdrawProposal(
            proposalId,
        );

    const isWithdrawing =
        withdrawMutation.isPending;

    function handleOpenChange(
        nextOpen: boolean,
    ) {
        if (isWithdrawing) {
            return;
        }

        setOpen(nextOpen);
    }

    function handleWithdraw() {
        withdrawMutation.mutate(
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
            onOpenChange={
                handleOpenChange
            }
        >
            <DialogTrigger asChild>
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                >
                    <RotateCcw className="size-4" />
                    Withdraw
                </Button>
            </DialogTrigger>

            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <div className="mb-2 flex size-11 items-center justify-center rounded-full bg-amber-500/10">
                        <TriangleAlert className="size-5 text-amber-400" />
                    </div>

                    <DialogTitle>
                        Withdraw proposal?
                    </DialogTitle>

                    <DialogDescription className="leading-6">
                        Your proposal for{" "}
                        <span className="font-medium text-foreground">
                            {projectTitle}
                        </span>{" "}
                        will be changed from
                        Pending to Withdrawn.
                    </DialogDescription>
                </DialogHeader>

                <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 text-sm leading-6 text-muted-foreground">
                    A withdrawn proposal cannot
                    be accepted by the client.
                    You also cannot submit a
                    second proposal for the same
                    project.
                </div>

                <DialogFooter className="gap-2 sm:gap-0">
                    <Button
                        type="button"
                        variant="outline"
                        disabled={isWithdrawing}
                        onClick={() => {
                            setOpen(false);
                        }}
                    >
                        Cancel
                    </Button>

                    <Button
                        type="button"
                        variant="destructive"
                        disabled={isWithdrawing}
                        onClick={handleWithdraw}
                    >
                        {isWithdrawing ? (
                            <>
                                <LoaderCircle className="size-4 animate-spin" />
                                Withdrawing
                            </>
                        ) : (
                            <>
                                <RotateCcw className="size-4" />
                                Withdraw proposal
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}