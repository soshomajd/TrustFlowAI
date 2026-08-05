"use client";

import { zodResolver } from
    "@hookform/resolvers/zod";
import {
    CircleDollarSign,
    Clock3,
    LoaderCircle,
} from "lucide-react";
import {
    useForm,
    useWatch,
} from "react-hook-form";

import { Button } from
    "@/components/ui/button";
import { Input } from
    "@/components/ui/input";
import { Label } from
    "@/components/ui/label";
import { Textarea } from
    "@/components/ui/textarea";
import { useCreateProposal } from
    "@/features/proposals/hooks/use-create-proposal";
import {
    createProposalSchema,
    type CreateProposalFormInput,
    type CreateProposalFormOutput,
} from
    "@/features/proposals/schemas/create-proposal-schema";
import { TriangleAlert, } from "lucide-react";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

type SubmitProposalFormProps = {
    projectId: string;

    projectBudget?: number;
    projectTitle?: string;

    onSubmitted: () => void;
    onCancel: () => void;
};

export function SubmitProposalForm({
    projectId,
    projectBudget,
    projectTitle,
    onSubmitted,
    onCancel,
}: SubmitProposalFormProps) {
    const createProposalMutation =
        useCreateProposal(projectId);

    const submitError =
        createProposalMutation.isError
            ? getApiErrorMessage(
                createProposalMutation.error,
                "Proposal could not be submitted.",
            )
            : null;

    const form = useForm<
        CreateProposalFormInput,
        unknown,
        CreateProposalFormOutput
    >({
        resolver: zodResolver(
            createProposalSchema,
        ),

        defaultValues: {
            coverLetter: "",
            bidAmount: "",
            estimatedDays: "",
        },
    });

    const coverLetter =
        useWatch({
            control: form.control,
            name: "coverLetter",
        }) ?? "";

    const isSubmitting =
        createProposalMutation.isPending;

    function handleSubmit(
        values:
            CreateProposalFormOutput,
    ) {
        createProposalMutation.mutate(
            values,
            {
                onSuccess: () => {
                    form.reset();
                    onSubmitted();
                },
            },
        );
    }

    return (
        <form
            noValidate
            className="space-y-5"
            onSubmit={form.handleSubmit(
                handleSubmit,
            )}
        >
            {(projectTitle ||
                projectBudget !==
                undefined) && (
                    <div className="rounded-xl border bg-muted/30 p-4">
                        {projectTitle && (
                            <div>
                                <p className="text-xs uppercase tracking-[0.16em] text-muted-foreground">
                                    Applying for
                                </p>

                                <p className="mt-1 font-semibold">
                                    {projectTitle}
                                </p>
                            </div>
                        )}

                        {projectBudget !==
                            undefined && (
                                <div className="mt-4 flex items-center gap-2 border-t pt-4 text-sm">
                                    <CircleDollarSign className="size-4 text-muted-foreground" />

                                    <span className="text-muted-foreground">
                                        Project budget
                                    </span>

                                    <span className="ml-auto font-semibold">
                                        {formatAmount(
                                            projectBudget,
                                        )}
                                    </span>
                                </div>
                            )}
                    </div>
                )}

            <div className="space-y-2">
                <div className="flex items-center justify-between gap-3">
                    <Label htmlFor="proposal-cover-letter">
                        Cover letter
                    </Label>

                    <span className="text-xs text-muted-foreground">
                        {coverLetter.length}/5000
                    </span>
                </div>

                <Textarea
                    id="proposal-cover-letter"
                    rows={8}
                    placeholder="Explain your experience, your approach and why you are a good fit for this project."
                    disabled={isSubmitting}
                    aria-invalid={Boolean(
                        form.formState.errors
                            .coverLetter,
                    )}
                    {...form.register(
                        "coverLetter",
                    )}
                />

                {form.formState.errors
                    .coverLetter?.message && (
                        <p className="text-sm text-destructive">
                            {
                                form.formState.errors
                                    .coverLetter.message
                            }
                        </p>
                    )}

                <p className="text-xs leading-5 text-muted-foreground">
                    Describe how you plan to
                    complete the project and
                    mention relevant experience.
                </p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
                <div className="space-y-2">
                    <Label htmlFor="proposal-bid-amount">
                        Bid amount
                    </Label>

                    <div className="relative">
                        <CircleDollarSign className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                        <Input
                            id="proposal-bid-amount"
                            type="text"
                            inputMode="decimal"
                            placeholder="2500.00"
                            className="pl-10"
                            disabled={isSubmitting}
                            aria-invalid={Boolean(
                                form.formState.errors
                                    .bidAmount,
                            )}
                            {...form.register(
                                "bidAmount",
                            )}
                        />
                    </div>

                    {form.formState.errors
                        .bidAmount?.message && (
                            <p className="text-sm text-destructive">
                                {
                                    form.formState.errors
                                        .bidAmount.message
                                }
                            </p>
                        )}
                </div>

                <div className="space-y-2">
                    <Label htmlFor="proposal-estimated-days">
                        Estimated days
                    </Label>

                    <div className="relative">
                        <Clock3 className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                        <Input
                            id="proposal-estimated-days"
                            type="text"
                            inputMode="numeric"
                            placeholder="14"
                            className="pl-10"
                            disabled={isSubmitting}
                            aria-invalid={Boolean(
                                form.formState.errors
                                    .estimatedDays,
                            )}
                            {...form.register(
                                "estimatedDays",
                            )}
                        />
                    </div>

                    {form.formState.errors
                        .estimatedDays
                        ?.message && (
                            <p className="text-sm text-destructive">
                                {
                                    form.formState.errors
                                        .estimatedDays
                                        .message
                                }
                            </p>
                        )}
                </div>
            </div>

            <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 text-sm leading-6 text-muted-foreground">
                Your proposal will be submitted
                with Pending status. You cannot
                submit another proposal for the
                same project.
            </div>

            {submitError && (
                <div
                    role="alert"
                    aria-live="assertive"
                    className="flex gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4"
                >
                    <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                    <div>
                        <p className="text-sm font-semibold text-destructive">
                            Could not submit proposal
                        </p>

                        <p className="mt-1 text-sm leading-6 text-muted-foreground">
                            {submitError}
                        </p>
                    </div>
                </div>
            )}

            <div className="flex flex-col-reverse gap-3 border-t pt-5 sm:flex-row sm:justify-end">
                <Button
                    type="button"
                    variant="outline"
                    disabled={isSubmitting}
                    onClick={onCancel}
                >
                    Cancel
                </Button>

                <Button
                    type="submit"
                    disabled={isSubmitting}
                >
                    {isSubmitting ? (
                        <>
                            <LoaderCircle className="size-4 animate-spin" />
                            Submitting proposal
                        </>
                    ) : (
                        "Submit proposal"
                    )}
                </Button>
            </div>
        </form>
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