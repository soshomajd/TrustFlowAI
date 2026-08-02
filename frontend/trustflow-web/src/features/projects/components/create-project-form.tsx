"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CalendarDays, CircleDollarSign, FileText, LoaderCircle, Save, X, } from "lucide-react";
import Link from "next/link";
import { useForm, useWatch, } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useCreateProject } from "@/features/projects/hooks/use-create-project";
import { createProjectSchema, type CreateProjectFormInput, type CreateProjectFormOutput, } from "@/features/projects/schemas/create-project-schema";

export function CreateProjectForm() {
    const createProjectMutation =
        useCreateProject();

    const {
        register,
        control,
        handleSubmit,
        formState: {
            errors,
        },
    } = useForm<
        CreateProjectFormInput,
        undefined,
        CreateProjectFormOutput
    >({
        resolver:
            zodResolver(
                createProjectSchema,
            ),

        defaultValues: {
            title: "",
            description: "",
            budget: "",
            deadline: "",
        },

        mode: "onBlur",
    });

    const title =
        useWatch({
            control,
            name: "title",
        }) ?? "";

    const description =
        useWatch({
            control,
            name: "description",
        }) ?? "";

    const isPending =
        createProjectMutation.isPending;

    function onSubmit(
        values: CreateProjectFormOutput,
    ) {
        createProjectMutation.mutate(
            values,
        );
    }

    return (
        <form
            onSubmit={handleSubmit(onSubmit)}
            noValidate
            className="rounded-2xl border bg-card/70 p-5 backdrop-blur sm:p-8"
        >
            <div className="grid gap-8">
                <div className="space-y-2">
                    <div className="flex items-center justify-between gap-4">
                        <Label htmlFor="title">
                            Project title
                        </Label>

                        <span className="text-xs text-muted-foreground">
                            {title.length}/200
                        </span>
                    </div>

                    <div className="relative">
                        <FileText className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                        <Input
                            id="title"
                            type="text"
                            autoComplete="off"
                            placeholder="Example: Build an AI-powered dashboard"
                            maxLength={200}
                            disabled={isPending}
                            aria-invalid={
                                Boolean(errors.title)
                            }
                            aria-describedby={
                                errors.title
                                    ? "title-error"
                                    : undefined
                            }
                            className="h-11 pl-10"
                            {...register("title")}
                        />
                    </div>

                    {errors.title && (
                        <p
                            id="title-error"
                            className="text-sm text-destructive"
                        >
                            {errors.title.message}
                        </p>
                    )}
                </div>

                <div className="space-y-2">
                    <div className="flex items-center justify-between gap-4">
                        <Label htmlFor="description">
                            Project description
                        </Label>

                        <span className="text-xs text-muted-foreground">
                            {description.length}/5000
                        </span>
                    </div>

                    <textarea
                        id="description"
                        rows={9}
                        maxLength={5000}
                        disabled={isPending}
                        placeholder="Describe the project goals, required features, technical requirements and expected result."
                        aria-invalid={
                            Boolean(
                                errors.description,
                            )
                        }
                        aria-describedby={
                            errors.description
                                ? "description-error"
                                : undefined
                        }
                        className="min-h-48 w-full resize-y rounded-md border border-input bg-transparent px-3 py-3 text-sm shadow-xs outline-none transition placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-destructive/20"
                        {...register(
                            "description",
                        )}
                    />

                    {errors.description && (
                        <p
                            id="description-error"
                            className="text-sm text-destructive"
                        >
                            {
                                errors.description
                                    .message
                            }
                        </p>
                    )}
                </div>

                <div className="grid gap-6 md:grid-cols-2">
                    <div className="space-y-2">
                        <Label htmlFor="budget">
                            Project budget
                        </Label>

                        <div className="relative">
                            <CircleDollarSign className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                            <Input
                                id="budget"
                                type="text"
                                inputMode="decimal"
                                autoComplete="off"
                                placeholder="5000.00"
                                disabled={isPending}
                                aria-invalid={
                                    Boolean(
                                        errors.budget,
                                    )
                                }
                                aria-describedby={
                                    errors.budget
                                        ? "budget-error"
                                        : "budget-help"
                                }
                                className="h-11 pl-10"
                                {...register("budget")}
                            />
                        </div>

                        {errors.budget ? (
                            <p
                                id="budget-error"
                                className="text-sm text-destructive"
                            >
                                {errors.budget.message}
                            </p>
                        ) : (
                            <p
                                id="budget-help"
                                className="text-xs text-muted-foreground"
                            >
                                Use a maximum of 2
                                decimal places.
                            </p>
                        )}
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="deadline">
                            Project deadline
                        </Label>

                        <div className="relative">
                            <CalendarDays className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                            <Input
                                id="deadline"
                                type="date"
                                min={
                                    getTodayDateInputValue()
                                }
                                disabled={isPending}
                                aria-invalid={
                                    Boolean(
                                        errors.deadline,
                                    )
                                }
                                aria-describedby={
                                    errors.deadline
                                        ? "deadline-error"
                                        : "deadline-help"
                                }
                                className="h-11 pl-10"
                                {...register(
                                    "deadline",
                                )}
                            />
                        </div>

                        {errors.deadline ? (
                            <p
                                id="deadline-error"
                                className="text-sm text-destructive"
                            >
                                {
                                    errors.deadline
                                        .message
                                }
                            </p>
                        ) : (
                            <p
                                id="deadline-help"
                                className="text-xs text-muted-foreground"
                            >
                                The selected day will end
                                at 23:59 in your local
                                timezone.
                            </p>
                        )}
                    </div>
                </div>

                <div className="flex flex-col-reverse gap-3 border-t pt-6 sm:flex-row sm:justify-end">
                    <Button
                        asChild
                        type="button"
                        variant="outline"
                    >
                        <Link href="/dashboard/client/projects">
                            <X className="size-4" />
                            Cancel
                        </Link>
                    </Button>

                    <Button
                        type="submit"
                        disabled={isPending}
                        className="shadow-blue-glow"
                    >
                        {isPending ? (
                            <>
                                <LoaderCircle className="size-4 animate-spin" />
                                Creating project...
                            </>
                        ) : (
                            <>
                                <Save className="size-4" />
                                Create project
                            </>
                        )}
                    </Button>
                </div>
            </div>
        </form>
    );
}

function getTodayDateInputValue() {
    const now = new Date();

    const timezoneOffset =
        now.getTimezoneOffset() *
        60_000;

    return new Date(
        now.getTime() -
        timezoneOffset,
    )
        .toISOString()
        .slice(0, 10);
}