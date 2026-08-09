"use client";

import { zodResolver } from
    "@hookform/resolvers/zod";
import {
    CalendarDays,
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
import {
    createMilestoneSchema,
    type CreateMilestoneFormInput,
    type CreateMilestoneFormOutput,
} from
    "@/features/milestones/schemas/create-milestone-schema";

type MilestoneFormProps = {
    projectDeadline: string;
    availableBudget: number;
    existingSequenceNumbers: number[];

    defaultValues:
    CreateMilestoneFormInput;

    submitLabel: string;
    submittingLabel: string;
    isSubmitting: boolean;

    onSubmit: (
        values:
            CreateMilestoneFormOutput,
    ) => void;

    onCancel: () => void;
};

export function MilestoneForm({
    projectDeadline,
    availableBudget,
    existingSequenceNumbers,
    defaultValues,
    submitLabel,
    submittingLabel,
    isSubmitting,
    onSubmit,
    onCancel,
}: MilestoneFormProps) {
    const schema =
        createMilestoneSchema({
            projectDeadline,

            remainingBudget:
                availableBudget,

            existingSequenceNumbers,
        });

    const form = useForm<
        CreateMilestoneFormInput,
        unknown,
        CreateMilestoneFormOutput
    >({
        resolver: zodResolver(schema),
        defaultValues,
    });

    const title =
        useWatch({
            control: form.control,
            name: "title",
        }) ?? "";

    const description =
        useWatch({
            control: form.control,
            name: "description",
        }) ?? "";

    return (
        <form
            noValidate
            className="space-y-5"
            onSubmit={form.handleSubmit(
                onSubmit,
            )}
        >
            <div className="rounded-xl border bg-muted/30 p-4">
                <div className="flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                    <div>
                        <p className="text-muted-foreground">
                            Available budget
                        </p>

                        <p className="mt-1 font-semibold">
                            {formatAmount(
                                availableBudget,
                            )}
                        </p>
                    </div>

                    <div className="sm:text-right">
                        <p className="text-muted-foreground">
                            Project deadline
                        </p>

                        <p className="mt-1 font-semibold">
                            {formatDate(
                                projectDeadline,
                            )}
                        </p>
                    </div>
                </div>
            </div>

            <div className="space-y-2">
                <div className="flex items-center justify-between gap-3">
                    <Label htmlFor="milestone-title">
                        Title
                    </Label>

                    <span className="text-xs text-muted-foreground">
                        {title.length}/200
                    </span>
                </div>

                <Input
                    id="milestone-title"
                    placeholder="Frontend implementation"
                    disabled={isSubmitting}
                    aria-invalid={Boolean(
                        form.formState.errors
                            .title,
                    )}
                    {...form.register("title")}
                />

                {form.formState.errors
                    .title?.message && (
                        <p className="text-sm text-destructive">
                            {
                                form.formState.errors
                                    .title.message
                            }
                        </p>
                    )}
            </div>

            <div className="min-w-0 max-w-full space-y-2">
                <div className="flex items-center justify-between gap-3">
                    <Label htmlFor="milestone-description">
                        Description
                    </Label>

                    <span className="text-xs text-muted-foreground">
                        {description.length}/5000
                    </span>
                </div>

                <Textarea
                    id="milestone-description"
                    placeholder="Describe what must be delivered."
                    rows={5}
                    wrap="soft"
                    disabled={isSubmitting}
                    className="w-full min-w-0 max-w-full resize-y overflow-x-hidden break-words [field-sizing:fixed]"
                    aria-invalid={Boolean(
                        form.formState.errors.description,
                    )}
                    {...form.register("description")}
                />

                {form.formState.errors
                    .description?.message && (
                        <p className="text-sm text-destructive">
                            {
                                form.formState.errors
                                    .description.message
                            }
                        </p>
                    )}
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
                <div className="space-y-2">
                    <Label htmlFor="milestone-amount">
                        Amount
                    </Label>

                    <Input
                        id="milestone-amount"
                        type="text"
                        inputMode="decimal"
                        placeholder="1500.00"
                        disabled={isSubmitting}
                        aria-invalid={Boolean(
                            form.formState.errors
                                .amount,
                        )}
                        {...form.register(
                            "amount",
                        )}
                    />

                    {form.formState.errors
                        .amount?.message && (
                            <p className="text-sm text-destructive">
                                {
                                    form.formState.errors
                                        .amount.message
                                }
                            </p>
                        )}
                </div>

                <div className="space-y-2">
                    <Label htmlFor="milestone-sequence">
                        Sequence number
                    </Label>

                    <Input
                        id="milestone-sequence"
                        type="text"
                        inputMode="numeric"
                        placeholder="1"
                        disabled={isSubmitting}
                        aria-invalid={Boolean(
                            form.formState.errors
                                .sequenceNumber,
                        )}
                        {...form.register(
                            "sequenceNumber",
                        )}
                    />

                    {form.formState.errors
                        .sequenceNumber
                        ?.message && (
                            <p className="text-sm text-destructive">
                                {
                                    form.formState.errors
                                        .sequenceNumber
                                        .message
                                }
                            </p>
                        )}
                </div>
            </div>

            <div className="space-y-2">
                <Label htmlFor="milestone-deadline">
                    Deadline
                </Label>

                <div className="relative">
                    <CalendarDays className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                    <Input
                        id="milestone-deadline"
                        type="date"
                        disabled={isSubmitting}
                        className="pl-10"
                        aria-invalid={Boolean(
                            form.formState.errors
                                .deadline,
                        )}
                        {...form.register(
                            "deadline",
                        )}
                    />
                </div>

                {form.formState.errors
                    .deadline?.message && (
                        <p className="text-sm text-destructive">
                            {
                                form.formState.errors
                                    .deadline.message
                            }
                        </p>
                    )}
            </div>

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
                            {submittingLabel}
                        </>
                    ) : (
                        submitLabel
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

function formatDate(
    value: string,
) {
    const date = new Date(value);

    if (
        Number.isNaN(date.getTime())
    ) {
        return "Unknown";
    }

    return new Intl.DateTimeFormat(
        "en-US",
        {
            dateStyle: "medium",
        },
    ).format(date);
}