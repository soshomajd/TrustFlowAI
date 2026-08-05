"use client";

import {
    BriefcaseBusiness,
    CalendarDays,
    ChevronLeft,
    ChevronRight,
    CircleDollarSign,
    Clock3,
    FolderKanban,
    LoaderCircle,
    RefreshCw,
    TriangleAlert,
    UserRound,
} from "lucide-react";
import { useState } from "react";

import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from "@/components/motion/animation-primitives";
import { Button } from "@/components/ui/button";
import { useAssignedProjects } from
    "@/features/projects/hooks/use-assigned-projects";
import type {
    AssignedProject,
    ProjectStatus,
} from "@/features/projects/types/project";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";
import Link from "next/link";

const PAGE_SIZE = 10;

export function AssignedProjectsScreen() {
    const [page, setPage] =
        useState(1);

    const projectsQuery =
        useAssignedProjects({
            page,
            pageSize: PAGE_SIZE,
        });

    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <AnimatedSection>
                <section className="rounded-2xl border bg-card/80 p-6 shadow-blue-glow backdrop-blur sm:p-8">
                    <div className="flex size-12 items-center justify-center rounded-xl bg-primary/10">
                        <BriefcaseBusiness className="size-6 text-electric" />
                    </div>

                    <p className="mt-6 text-sm font-medium uppercase tracking-[0.25em] text-electric">
                        Freelancer workspace
                    </p>

                    <h1 className="mt-3 text-3xl font-semibold">
                        Assigned projects
                    </h1>

                    <p className="mt-3 max-w-2xl text-muted-foreground">
                        View projects where your
                        proposal has been accepted
                        and you have been assigned
                        as the freelancer.
                    </p>
                </section>
            </AnimatedSection>

            <AssignedProjectsResults
                query={projectsQuery}
                onPrevious={() => {
                    setPage((currentPage) =>
                        Math.max(
                            1,
                            currentPage - 1,
                        ),
                    );
                }}
                onNext={() => {
                    setPage((currentPage) =>
                        currentPage + 1,
                    );
                }}
            />
        </main>
    );
}

type AssignedProjectsResultsProps = {
    query:
    ReturnType<
        typeof useAssignedProjects
    >;

    onPrevious: () => void;
    onNext: () => void;
};

function AssignedProjectsResults({
    query,
    onPrevious,
    onNext,
}: AssignedProjectsResultsProps) {
    if (query.isPending) {
        return (
            <AssignedProjectsSkeleton />
        );
    }

    if (query.isError) {
        return (
            <AssignedProjectsError
                message={getApiErrorMessage(
                    query.error,
                    "Assigned projects could not be loaded.",
                )}
                isRetrying={query.isFetching}
                onRetry={() => {
                    void query.refetch();
                }}
            />
        );
    }

    const response = query.data;

    if (
        !response ||
        response.items.length === 0
    ) {
        return (
            <AssignedProjectsEmpty />
        );
    }

    return (
        <AnimatedSection className="mt-6">
            <section className="overflow-hidden rounded-2xl border bg-card/60 backdrop-blur">
                <header className="flex flex-col gap-3 border-b p-5 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                        <h2 className="text-lg font-semibold">
                            Active assignments
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            {response.totalItems}{" "}
                            {response.totalItems === 1
                                ? "assigned project"
                                : "assigned projects"}
                        </p>
                    </div>

                    {query.isFetching && (
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            <LoaderCircle className="size-4 animate-spin" />
                            Updating projects
                        </div>
                    )}
                </header>

                <StaggerContainer className="divide-y">
                    {response.items.map(
                        (project) => (
                            <StaggerItem
                                key={project.id}
                            >
                                <AssignedProjectCard
                                    project={project}
                                />
                            </StaggerItem>
                        ),
                    )}
                </StaggerContainer>

                {response.totalPages > 1 && (
                    <footer className="flex flex-col gap-3 border-t p-5 sm:flex-row sm:items-center sm:justify-between">
                        <p className="text-sm text-muted-foreground">
                            Page {response.page} of{" "}
                            {response.totalPages}
                        </p>

                        <div className="flex items-center gap-2">
                            <Button
                                type="button"
                                variant="outline"
                                disabled={
                                    !response.hasPreviousPage ||
                                    query.isFetching
                                }
                                onClick={onPrevious}
                            >
                                <ChevronLeft className="size-4" />
                                Previous
                            </Button>

                            <Button
                                type="button"
                                variant="outline"
                                disabled={
                                    !response.hasNextPage ||
                                    query.isFetching
                                }
                                onClick={onNext}
                            >
                                Next
                                <ChevronRight className="size-4" />
                            </Button>
                        </div>
                    </footer>
                )}
            </section>
        </AnimatedSection>
    );
}

function AssignedProjectCard({
    project,
}: {
    project: AssignedProject;
}) {
    const allocationPercentage =
        project.budget <= 0
            ? 0
            : Math.min(
                100,
                Math.round(
                    (project.allocatedAmount /
                        project.budget) *
                    100,
                ),
            );

    return (
        <article className="p-5 transition hover:bg-muted/20 sm:p-6">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-3">
                        <div className="flex items-center gap-2">
                            <FolderKanban className="size-5 text-electric" />

                            <h3 className="text-lg font-semibold">
                                {project.title}
                            </h3>
                        </div>

                        <ProjectStatusBadge
                            status={project.status}
                        />
                    </div>

                    <p className="mt-3 line-clamp-3 max-w-3xl text-sm leading-6 text-muted-foreground">
                        {project.description}
                    </p>

                    <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 text-xs text-muted-foreground">
                        <span className="flex items-center gap-2">
                            <UserRound className="size-3.5" />
                            Client:{" "}
                            {project.clientFullName ||
                                "Unknown client"}
                        </span>

                        <span className="flex items-center gap-2">
                            <Clock3 className="size-3.5" />
                            Assigned project created{" "}
                            {formatDate(
                                project.createdAt,
                            )}
                        </span>
                    </div>
                </div>

                <div className="grid shrink-0 gap-3 sm:grid-cols-2 lg:min-w-64 lg:grid-cols-1">
                    <ProjectMeta
                        icon={CircleDollarSign}
                        label="Project budget"
                        value={formatAmount(
                            project.budget,
                        )}
                    />

                    <ProjectMeta
                        icon={CalendarDays}
                        label="Deadline"
                        value={formatDate(
                            project.deadline,
                        )}
                    />
                </div>
            </div>

            <div className="mt-5 grid gap-4 border-t pt-5 md:grid-cols-2">
                <div className="rounded-xl border bg-background/30 p-4">
                    <div className="flex items-center justify-between gap-4">
                        <div>
                            <p className="text-xs text-muted-foreground">
                                Milestones
                            </p>

                            <p className="mt-1 text-sm font-medium">
                                {project.milestoneCount}{" "}
                                {project.milestoneCount ===
                                    1
                                    ? "milestone"
                                    : "milestones"}
                            </p>
                        </div>

                        <FolderKanban className="size-5 text-electric" />
                    </div>
                </div>

                <div className="rounded-xl border bg-background/30 p-4">
                    <div className="flex items-center justify-between gap-4">
                        <div>
                            <p className="text-xs text-muted-foreground">
                                Budget allocated
                            </p>

                            <p className="mt-1 text-sm font-medium">
                                {formatAmount(
                                    project.allocatedAmount,
                                )}{" "}
                                of{" "}
                                {formatAmount(
                                    project.budget,
                                )}
                            </p>
                        </div>

                        <span className="text-sm font-semibold text-electric">
                            {allocationPercentage}%
                        </span>
                    </div>

                    <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted">
                        <div
                            className="h-full rounded-full bg-primary transition-[width]"
                            style={{
                                width: `${allocationPercentage}%`,
                            }}
                        />
                    </div>
                </div>
            </div>

            <div className="mt-5 flex justify-end border-t pt-5">
                <Button
                    asChild
                    variant="outline"
                >
                    <Link
                        href={`/dashboard/freelancer/projects/${project.id}`}
                    >
                        Open workspace
                    </Link>
                </Button>
            </div>
        </article>
    );
}

type ProjectMetaProps = {
    icon: typeof CalendarDays;
    label: string;
    value: string;
};

function ProjectMeta({
    icon: Icon,
    label,
    value,
}: ProjectMetaProps) {
    return (
        <div className="rounded-xl border bg-background/30 px-4 py-3">
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

function ProjectStatusBadge({
    status,
}: {
    status: ProjectStatus;
}) {
    return (
        <span
            className={cn(
                "rounded-full border px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wider",
                projectStatusStyles[
                status
                ],
            )}
        >
            {formatProjectStatus(status)}
        </span>
    );
}

const projectStatusStyles: Record<
    ProjectStatus,
    string
> = {
    Open:
        "border-cyan-500/30 bg-cyan-500/10 text-cyan-400",

    InProgress:
        "border-blue-500/30 bg-blue-500/10 text-blue-400",

    Completed:
        "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",

    Cancelled:
        "border-red-500/30 bg-red-500/10 text-red-400",
};

function AssignedProjectsEmpty() {
    return (
        <section className="mt-6 rounded-2xl border border-dashed bg-card/40 p-10 text-center">
            <BriefcaseBusiness className="mx-auto size-10 text-muted-foreground" />

            <h2 className="mt-4 text-lg font-semibold">
                No assigned projects yet
            </h2>

            <p className="mx-auto mt-2 max-w-lg text-sm leading-6 text-muted-foreground">
                Projects will appear here
                after a client accepts one of
                your proposals.
            </p>
        </section>
    );
}

function AssignedProjectsError({
    message,
    isRetrying,
    onRetry,
}: {
    message: string;
    isRetrying: boolean;
    onRetry: () => void;
}) {
    return (
        <section className="mt-6 rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex gap-3">
                    <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                    <div>
                        <h2 className="font-semibold">
                            Could not load assigned
                            projects
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            {message}
                        </p>
                    </div>
                </div>

                <Button
                    type="button"
                    variant="outline"
                    disabled={isRetrying}
                    onClick={onRetry}
                >
                    {isRetrying ? (
                        <LoaderCircle className="size-4 animate-spin" />
                    ) : (
                        <RefreshCw className="size-4" />
                    )}

                    {isRetrying
                        ? "Trying again"
                        : "Try again"}
                </Button>
            </div>
        </section>
    );
}

export function AssignedProjectsPageSkeleton() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <div className="h-64 animate-pulse rounded-2xl border bg-muted/30" />

            <AssignedProjectsSkeleton />
        </main>
    );
}

function AssignedProjectsSkeleton() {
    return (
        <section className="mt-6 animate-pulse overflow-hidden rounded-2xl border bg-card/60">
            <div className="border-b p-5">
                <div className="h-6 w-48 rounded bg-muted" />
                <div className="mt-3 h-4 w-32 rounded bg-muted" />
            </div>

            <div className="divide-y">
                {Array.from({
                    length: 3,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="h-80 bg-muted/20 p-5"
                    />
                ))}
            </div>
        </section>
    );
}

function formatProjectStatus(
    status: ProjectStatus,
) {
    return status === "InProgress"
        ? "In progress"
        : status;
}

function formatAmount(
    value: number,
) {
    return new Intl.NumberFormat(
        "en-US",
        {
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