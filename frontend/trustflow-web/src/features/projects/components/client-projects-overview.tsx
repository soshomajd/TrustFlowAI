"use client";

import {
    AlertCircle,
    ArrowRight,
    CalendarDays,
    FolderKanban,
    LoaderCircle,
    RefreshCw,
} from "lucide-react";
import Link from "next/link";

import { Button } from
    "@/components/ui/button";
import { useClientProjects } from
    "@/features/projects/hooks/use-client-projects";
import type {
    ClientProject,
    ProjectStatus,
} from "@/features/projects/types/project";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";

export function ClientProjectsOverview() {
    const projectsQuery =
        useClientProjects({
            page: 1,
            pageSize: 5,
        });

    if (projectsQuery.isPending) {
        return <ProjectsLoadingState />;
    }

    if (projectsQuery.isError) {
        return (
            <ProjectsErrorState
                error={projectsQuery.error}
                isRetrying={
                    projectsQuery.isFetching
                }
                onRetry={() => {
                    void projectsQuery.refetch();
                }}
            />
        );
    }

    const projects =
        projectsQuery.data.items;

    if (projects.length === 0) {
        return <ProjectsEmptyState />;
    }

    return (
        <section className="rounded-2xl border bg-card/70 backdrop-blur">
            <header className="flex flex-col gap-4 border-b p-5 sm:flex-row sm:items-center sm:justify-between">
                <div>
                    <p className="text-sm font-semibold">
                        Recent projects
                    </p>

                    <p className="mt-1 text-sm text-muted-foreground">
                        {projectsQuery.data.totalItems}{" "}
                        projects in total
                    </p>
                </div>

                <Button
                    asChild
                    variant="outline"
                    size="sm"
                >
                    <Link href="/dashboard/client/projects">
                        View all projects
                        <ArrowRight className="size-4" />
                    </Link>
                </Button>
            </header>

            <div className="divide-y">
                {projects.map((project) => (
                    <ProjectRow
                        key={project.id}
                        project={project}
                    />
                ))}
            </div>

            {projectsQuery.isFetching && (
                <div className="flex items-center gap-2 border-t px-5 py-3 text-xs text-muted-foreground">
                    <LoaderCircle className="size-3.5 animate-spin" />

                    Updating projects...
                </div>
            )}
        </section>
    );
}


function ProjectsLoadingState() {
    return (
        <section className="rounded-2xl border bg-card/70 p-5">
            <div className="flex items-center gap-3">
                <div className="h-5 w-32 animate-pulse rounded bg-muted" />

                <LoaderCircle className="ml-auto size-4 animate-spin text-muted-foreground" />
            </div>

            <div className="mt-6 space-y-4">
                {Array.from({
                    length: 3,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="rounded-xl border p-4"
                    >
                        <div className="h-4 w-1/3 animate-pulse rounded bg-muted" />

                        <div className="mt-3 h-3 w-2/3 animate-pulse rounded bg-muted" />

                        <div className="mt-5 h-2 w-full animate-pulse rounded bg-muted" />
                    </div>
                ))}
            </div>
        </section>
    );
}


type ProjectsErrorStateProps = {
    error: unknown;
    isRetrying: boolean;
    onRetry: () => void;
};

function ProjectsErrorState({
    error,
    isRetrying,
    onRetry,
}: ProjectsErrorStateProps) {
    const errorMessage =
        getApiErrorMessage(
            error,
            "Projects could not be loaded.",
        );

    return (
        <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
            <div className="flex size-11 items-center justify-center rounded-xl bg-destructive/10">
                <AlertCircle className="size-5 text-destructive" />
            </div>

            <h2 className="mt-4 font-semibold">
                Could not load projects
            </h2>

            <p className="mt-2 max-w-xl text-sm text-muted-foreground">
                {errorMessage}
            </p>

            <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={isRetrying}
                onClick={onRetry}
                className="mt-5"
            >
                {isRetrying ? (
                    <LoaderCircle className="size-4 animate-spin" />
                ) : (
                    <RefreshCw className="size-4" />
                )}

                {isRetrying
                    ? "Trying again..."
                    : "Try again"}
            </Button>
        </section>
    );
}

function ProjectsEmptyState() {
    return (
        <section className="rounded-2xl border border-dashed bg-card/50 p-8 text-center">
            <div className="mx-auto flex size-14 items-center justify-center rounded-2xl border border-primary/30 bg-primary/10">
                <FolderKanban className="size-6 text-electric" />
            </div>

            <h2 className="mt-5 text-lg font-semibold">
                No projects yet
            </h2>

            <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
                Create your first project and start
                receiving proposals from freelancers.
            </p>

            <Button
                asChild
                className="mt-6"
            >
                <Link href="/dashboard/client/projects/new">
                    Create first project
                    <ArrowRight className="size-4" />
                </Link>
            </Button>
        </section>
    );
}

type ProjectRowProps = {
    project: ClientProject;
};

function ProjectRow({
    project,
}: ProjectRowProps) {
    const progress =
        project.milestoneCount === 0
            ? 0
            : Math.round(
                (project.approvedMilestoneCount /
                    project.milestoneCount) *
                100,
            );

    return (
        <article className="p-5 transition hover:bg-muted/20">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-3">
                        <Link
                            href={`/dashboard/client/projects/${project.id}`}
                            className="truncate font-semibold transition hover:text-electric"
                        >
                            {project.title}
                        </Link>

                        <ProjectStatusBadge
                            status={project.status}
                        />
                    </div>

                    <p className="mt-2 line-clamp-2 max-w-2xl text-sm text-muted-foreground">
                        {project.description}
                    </p>
                </div>

                <div className="shrink-0 text-left lg:text-right">
                    <p className="text-sm font-semibold">
                        {formatAmount(project.budget)}
                    </p>

                    <p className="mt-1 text-xs text-muted-foreground">
                        Project budget
                    </p>
                </div>
            </div>

            <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <ProjectInformation
                    label="Deadline"
                    value={formatDate(
                        project.deadline,
                    )}
                    icon={CalendarDays}
                />

                <ProjectInformation
                    label="Proposals"
                    value={`${project.proposalCount}`}
                    icon={FolderKanban}
                />

                <ProjectInformation
                    label="Pending proposals"
                    value={`${project.pendingProposalCount}`}
                    icon={FolderKanban}
                />

                <ProjectInformation
                    label="Milestones"
                    value={`${project.approvedMilestoneCount}/${project.milestoneCount}`}
                    icon={FolderKanban}
                />
            </div>

            <div className="mt-5">
                <div className="mb-2 flex items-center justify-between text-xs">
                    <span className="text-muted-foreground">
                        Milestone progress
                    </span>

                    <span className="font-medium">
                        {progress}%
                    </span>
                </div>

                <div className="h-2 overflow-hidden rounded-full bg-muted">
                    <div
                        className="h-full rounded-full bg-primary transition-[width]"
                        style={{
                            width: `${progress}%`,
                        }}
                    />
                </div>
            </div>
        </article>
    );
}

type ProjectInformationProps = {
    label: string;
    value: string;
    icon: typeof FolderKanban;
};

function ProjectInformation({
    label,
    value,
    icon: Icon,
}: ProjectInformationProps) {
    return (
        <div className="flex items-center gap-3">
            <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                <Icon className="size-4 text-electric" />
            </div>

            <div>
                <p className="text-xs text-muted-foreground">
                    {label}
                </p>

                <p className="mt-0.5 text-sm font-medium">
                    {value}
                </p>
            </div>
        </div>
    );
}

type ProjectStatusBadgeProps = {
    status: ProjectStatus;
};

function ProjectStatusBadge({
    status,
}: ProjectStatusBadgeProps) {
    return (
        <span
            className={cn(
                "rounded-full border px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wider",
                statusStyles[status],
            )}
        >
            {formatStatus(status)}
        </span>
    );
}

const statusStyles: Record<
    ProjectStatus,
    string
> = {
    Open:
        "border-cyan-500/30 bg-cyan-500/10 text-cyan-400",

    InProgress:
        "border-blue-500/30 bg-blue-500/10 text-blue-400",

    Completed:
        "border-green-500/30 bg-green-500/10 text-green-400",

    Cancelled:
        "border-red-500/30 bg-red-500/10 text-red-400",
};

function formatStatus(
    status: ProjectStatus,
) {
    if (status === "InProgress") {
        return "In progress";
    }

    return status;
}

function formatDate(value: string) {
    return new Intl.DateTimeFormat(
        "en-US",
        {
            dateStyle: "medium",
        },
    ).format(new Date(value));
}

function formatAmount(value: number) {
    return new Intl.NumberFormat(
        "en-US",
        {
            maximumFractionDigits: 2,
        },
    ).format(value);
}