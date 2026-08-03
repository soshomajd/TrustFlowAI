"use client";

import {
    CalendarDays,
    CircleDollarSign,
    FolderKanban,
    LoaderCircle,
    RefreshCw,
    TriangleAlert,
} from "lucide-react";
import { AnimatedSection, StaggerContainer, StaggerItem, } from "@/components/motion/animation-primitives";
import { Button } from "@/components/ui/button";
import { useProjectMilestones } from "@/features/milestones/hooks/use-project-milestones";
import type { MilestoneStatus, ProjectMilestone, } from "@/features/milestones/types/milestone";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";
import { CreateMilestoneDialog } from "@/features/milestones/components/create-milestone-dialog";
import type { ProjectStatus } from "@/features/projects/types/project";
import { DeleteMilestoneDialog } from "@/features/milestones/components/delete-milestone-dialog";
import { EditMilestoneDialog } from "@/features/milestones/components/edit-milestone-dialog";

type ProjectMilestonesSectionProps = {
    projectId: string;
    projectBudget: number;
    projectDeadline: string;
    projectStatus: ProjectStatus;
};


export function ProjectMilestonesSection({
    projectId,
    projectBudget,
    projectDeadline,
    projectStatus,
}: ProjectMilestonesSectionProps) {
    const milestonesQuery =
        useProjectMilestones(projectId);

    if (milestonesQuery.isLoading) {
        return (
            <ProjectMilestonesSkeleton />
        );
    }

    if (milestonesQuery.isError) {
        return (
            <ProjectMilestonesError
                message={getApiErrorMessage(
                    milestonesQuery.error,
                    "Milestones could not be loaded.",
                )}
                onRetry={() => {
                    void milestonesQuery.refetch();
                }}
            />
        );
    }

    const milestones =
        milestonesQuery.data ?? [];
    const allocatedAmount =
        milestones.reduce(
            (total, milestone) =>
                total + milestone.amount,
            0,
        );

    const remainingBudget =
        roundCurrency(
            Math.max(
                0,
                projectBudget -
                allocatedAmount,
            ),
        );
    const existingSequenceNumbers =
        milestones.map(
            (milestone) =>
                milestone.sequenceNumber,
        );

    return (
        <AnimatedSection>
            <section
                aria-labelledby="project-milestones-title"
                className="rounded-2xl border bg-card/70 p-5 backdrop-blur sm:p-6"
            >
                <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                        <div className="flex items-center gap-2">
                            <FolderKanban className="size-5 text-electric" />

                            <h2
                                id="project-milestones-title"
                                className="text-lg font-semibold"
                            >
                                Milestones
                            </h2>
                        </div>

                        <p className="mt-2 text-sm text-muted-foreground">
                            Project delivery stages
                            ordered by sequence.
                        </p>
                    </div>

                    <div className="flex flex-wrap items-center gap-3">
                        <span className="rounded-full border px-3 py-1 text-xs font-medium text-muted-foreground">
                            {milestones.length}{" "}
                            {milestones.length === 1
                                ? "milestone"
                                : "milestones"}
                        </span>

                        {milestonesQuery.isFetching && (
                            <div className="flex items-center gap-2 text-xs text-muted-foreground">
                                <LoaderCircle className="size-3.5 animate-spin" />
                                Updating
                            </div>
                        )}

                        <CreateMilestoneDialog
                            projectId={projectId}
                            projectDeadline={
                                projectDeadline
                            }
                            projectStatus={
                                projectStatus
                            }
                            remainingBudget={
                                remainingBudget
                            }
                            existingSequenceNumbers={
                                existingSequenceNumbers
                            }
                        />
                    </div>
                </header>

                {milestones.length === 0 ? (
                    <ProjectMilestonesEmpty />
                ) : (
                    <StaggerContainer className="mt-6 space-y-3">
                        {milestones.map(
                            (milestone) => (
                                <StaggerItem
                                    key={milestone.id}
                                ><MilestoneCard
                                        projectId={projectId}
                                        projectStatus={
                                            projectStatus
                                        }
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
                                    />
                                </StaggerItem>
                            ),
                        )}
                    </StaggerContainer>
                )}
            </section>
        </AnimatedSection>
    );
}

function MilestoneCard({
    projectId,
    projectStatus,
    projectDeadline,
    remainingBudget,
    existingSequenceNumbers,
    milestone,
}: {
    projectId: string;
    projectStatus: ProjectStatus;
    projectDeadline: string;
    remainingBudget: number;
    existingSequenceNumbers: number[];
    milestone: ProjectMilestone;
}) {
    const canManageMilestone =
        projectStatus === "Open" &&
        milestone.status === "Pending";
    return (
        <article className="group rounded-xl border bg-background/40 p-4 transition hover:border-primary/40 hover:bg-background/70 sm:p-5">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0">
                    <div className="flex items-start justify-between gap-4">
                        <div className="flex flex-wrap items-center gap-3">
                            <span className="text-xs font-medium uppercase tracking-[0.18em] text-muted-foreground">
                                Milestone{" "}
                                {milestone.sequenceNumber}
                            </span>

                            <MilestoneStatusBadge
                                status={milestone.status}
                            />
                        </div>

                        {canManageMilestone && (
                            <div className="flex shrink-0 items-center">
                                <EditMilestoneDialog
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
                                />

                                <DeleteMilestoneDialog
                                    projectId={projectId}
                                    milestone={milestone}
                                />
                            </div>
                        )}
                    </div>

                    <h3 className="mt-3 wrap-break-word font-semibold">
                        {milestone.title}
                    </h3>

                    <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                        {milestone.description}
                    </p>
                </div>

                <div className="grid shrink-0 gap-3 sm:grid-cols-2 lg:min-w-64 lg:grid-cols-1">
                    <MilestoneMeta
                        icon={CircleDollarSign}
                        label="Amount"
                        value={formatAmount(
                            milestone.amount,
                        )}
                    />

                    <MilestoneMeta
                        icon={CalendarDays}
                        label="Deadline"
                        value={formatDate(
                            milestone.deadline,
                        )}
                    />
                </div>
            </div>
        </article>
    );
}

type MilestoneMetaProps = {
    icon: typeof CalendarDays;
    label: string;
    value: string;
};

function MilestoneMeta({
    icon: Icon,
    label,
    value,
}: MilestoneMetaProps) {
    return (
        <div className="rounded-lg border bg-card/50 px-3 py-2">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <Icon className="size-3.5" />
                {label}
            </div>

            <p className="mt-1 text-sm font-medium">
                {value}
            </p>
        </div>
    );
}

function MilestoneStatusBadge({
    status,
}: {
    status: MilestoneStatus;
}) {
    return (
        <span
            className={cn(
                "rounded-full border px-2.5 py-1 text-xs font-medium",
                milestoneStatusStyles[
                status
                ],
            )}
        >
            {formatLabel(status)}
        </span>
    );
}

const milestoneStatusStyles: Record<
    MilestoneStatus,
    string
> = {
    Pending:
        "border-slate-500/30 bg-slate-500/10 text-slate-300",

    InProgress:
        "border-blue-500/30 bg-blue-500/10 text-blue-400",

    Submitted:
        "border-amber-500/30 bg-amber-500/10 text-amber-400",

    Approved:
        "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",

    Rejected:
        "border-red-500/30 bg-red-500/10 text-red-400",
};

function ProjectMilestonesEmpty() {
    return (
        <div className="mt-6 rounded-xl border border-dashed p-8 text-center">
            <FolderKanban className="mx-auto size-8 text-muted-foreground" />

            <p className="mt-3 font-medium">
                No milestones yet
            </p>

            <p className="mx-auto mt-1 max-w-md text-sm leading-6 text-muted-foreground">
                Milestones can be created
                while the project is open.
            </p>
        </div>
    );
}

function ProjectMilestonesError({
    message,
    onRetry,
}: {
    message: string;
    onRetry: () => void;
}) {
    return (
        <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex gap-3">
                    <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                    <div>
                        <h2 className="font-semibold">
                            Could not load milestones
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            {message}
                        </p>
                    </div>
                </div>

                <Button
                    type="button"
                    variant="outline"
                    onClick={onRetry}
                >
                    <RefreshCw className="size-4" />
                    Try again
                </Button>
            </div>
        </section>
    );
}

function ProjectMilestonesSkeleton() {
    return (
        <section className="animate-pulse rounded-2xl border bg-card/70 p-5 sm:p-6">
            <div className="flex items-start justify-between gap-4">
                <div>
                    <div className="h-6 w-32 rounded bg-muted" />
                    <div className="mt-3 h-4 w-64 rounded bg-muted" />
                </div>

                <div className="h-7 w-24 rounded-full bg-muted" />
            </div>

            <div className="mt-6 space-y-3">
                {Array.from({
                    length: 3,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="h-36 rounded-xl border bg-muted/40"
                    />
                ))}
            </div>
        </section>
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
    return new Intl.DateTimeFormat(
        "en-US",
        {
            dateStyle: "medium",
        },
    ).format(new Date(value));
}

function formatLabel(
    value: string,
) {
    return value.replace(
        /([a-z])([A-Z])/g,
        "$1 $2",
    );
}

function roundCurrency(
    value: number,
) {
    return (
        Math.round(
            value * 100,
        ) / 100
    );
}