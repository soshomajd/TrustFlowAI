"use client";

import { useEffect } from "react";
import {
    AlertCircle,
    CalendarDays,
    CheckCircle2,
    CircleDollarSign,
    FolderKanban,
    LoaderCircle,
    RefreshCw,
    Send,
    UserRound,
} from "lucide-react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ClientProjectsListSkeleton } from "@/features/projects/components/client-projects-list-skeleton";
import { useClientProjects } from "@/features/projects/hooks/use-client-projects";
import { useClientProjectsSearchParams } from "@/features/projects/hooks/use-client-projects-search-params";
import type { ClientProject, ProjectStatus, } from "@/features/projects/types/project";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";
import { StaggerContainer, StaggerItem, } from "@/components/motion/animation-primitives";

const PAGE_SIZE = 10;

export function ClientProjectsListSection() {
    const {
        page,
        status,
        setPage,
    } = useClientProjectsSearchParams();

    const projectsQuery =
        useClientProjects({
            page,
            pageSize: PAGE_SIZE,
            status,
        });

    useEffect(() => {
        if (
            !projectsQuery.data ||
            projectsQuery.isPlaceholderData
        ) {
            return;
        }

        const totalPages =
            projectsQuery.data.totalPages;

        if (
            totalPages === 0 &&
            page !== 1
        ) {
            setPage(1);
            return;
        }

        if (
            totalPages > 0 &&
            page > totalPages
        ) {
            setPage(totalPages);
        }
    }, [
        page,
        projectsQuery.data,
        projectsQuery.isPlaceholderData,
        setPage,
    ]);

    if (projectsQuery.isPending) {
        return (
            <ClientProjectsListSkeleton />
        );
    }

    if (projectsQuery.isError) {
        return (
            <ProjectsListError
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

    const response =
        projectsQuery.data;

    if (response.items.length === 0) {
        return (
            <ProjectsListEmptyState
                status={status}
            />
        );
    }

    return (
        <section className="relative mt-6 overflow-hidden rounded-2xl border bg-card/70 backdrop-blur">
            <header className="flex flex-col gap-3 border-b p-5 sm:flex-row sm:items-center sm:justify-between">
                <div>
                    <h2 className="font-semibold">
                        Project list
                    </h2>

                    <p className="mt-1 text-sm text-muted-foreground">
                        {formatNumber(
                            response.totalItems,
                        )}{" "}
                        projects found
                    </p>
                </div>

                <div className="flex items-center gap-3 text-xs text-muted-foreground">
                    {projectsQuery.isFetching && (
                        <span className="flex items-center gap-2">
                            <LoaderCircle className="size-3.5 animate-spin" />
                            Updating
                        </span>
                    )}

                    <span>
                        Page {response.page} of{" "}
                        {Math.max(
                            response.totalPages,
                            1,
                        )}
                    </span>
                </div>
            </header>

            <StaggerContainer
                key={`${response.page}-${status ?? "all"}`}
                className="divide-y"
            >
                {response.items.map(
                    (project) => (
                        <StaggerItem
                            key={project.id}
                        >
                            <ClientProjectListItem
                                project={project}
                            />
                        </StaggerItem>
                    ),
                )}
            </StaggerContainer>

            <ProjectsPagination
                page={response.page}
                totalPages={
                    response.totalPages
                }
                hasPreviousPage={
                    response.hasPreviousPage
                }
                hasNextPage={
                    response.hasNextPage
                }
                disabled={
                    projectsQuery.isPlaceholderData
                }
                onPrevious={() => {
                    setPage(page - 1);
                }}
                onNext={() => {
                    setPage(page + 1);
                }}
            />
        </section>
    );
}


type ClientProjectListItemProps = {
    project: ClientProject;
};

function ClientProjectListItem({
    project,
}: ClientProjectListItemProps) {
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
                        <h3 className="font-semibold">
                            <Link
                                href={`/dashboard/client/projects/${project.id}`}
                                className="transition hover:text-electric"
                            >
                                {project.title}
                            </Link>
                        </h3>

                        <ProjectStatusBadge
                            status={project.status}
                        />
                    </div>

                    <p className="mt-2 line-clamp-2 max-w-3xl text-sm leading-6 text-muted-foreground">
                        {project.description}
                    </p>
                </div>

                <div className="shrink-0 lg:text-right">
                    <p className="text-lg font-semibold">
                        {formatAmount(
                            project.budget,
                        )}
                    </p>

                    <p className="text-xs text-muted-foreground">
                        Project budget
                    </p>
                </div>
            </div>

            <div className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <ProjectDetail
                    label="Deadline"
                    value={formatDate(
                        project.deadline,
                    )}
                    icon={CalendarDays}
                />

                <ProjectDetail
                    label="Freelancer"
                    value={
                        project.freelancerFullName ??
                        "Not assigned"
                    }
                    icon={UserRound}
                />

                <ProjectDetail
                    label="Proposals"
                    value={`${project.proposalCount} total, ${project.pendingProposalCount} pending`}
                    icon={Send}
                />

                <ProjectDetail
                    label="Allocated"
                    value={formatAmount(
                        project.allocatedAmount,
                    )}
                    icon={CircleDollarSign}
                />
            </div>

            <div className="mt-5">
                <div className="mb-2 flex items-center justify-between gap-4 text-xs">
                    <span className="flex items-center gap-2 text-muted-foreground">
                        <CheckCircle2 className="size-3.5" />

                        {project.approvedMilestoneCount}{" "}
                        of {project.milestoneCount}{" "}
                        milestones approved
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

type ProjectDetailProps = {
    label: string;
    value: string;
    icon: typeof FolderKanban;
};

function ProjectDetail({
    label,
    value,
    icon: Icon,
}: ProjectDetailProps) {
    return (
        <div className="flex items-center gap-3">
            <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                <Icon className="size-4 text-electric" />
            </div>

            <div className="min-w-0">
                <p className="text-xs text-muted-foreground">
                    {label}
                </p>

                <p className="mt-0.5 truncate text-sm font-medium">
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
                projectStatusStyles[status],
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
        "border-green-500/30 bg-green-500/10 text-green-400",

    Cancelled:
        "border-red-500/30 bg-red-500/10 text-red-400",
};

function formatProjectStatus(
    status: ProjectStatus,
) {
    return status === "InProgress"
        ? "In progress"
        : status;
}

type ProjectsPaginationProps = {
    page: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    disabled: boolean;
    onPrevious: () => void;
    onNext: () => void;
};

function ProjectsPagination({
    page,
    totalPages,
    hasPreviousPage,
    hasNextPage,
    disabled,
    onPrevious,
    onNext,
}: ProjectsPaginationProps) {
    return (
        <footer className="flex flex-col gap-4 border-t p-5 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-muted-foreground">
                Page {page} of{" "}
                {Math.max(totalPages, 1)}
            </p>

            <div className="flex gap-2">
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={
                        disabled ||
                        !hasPreviousPage
                    }
                    onClick={onPrevious}
                >
                    Previous
                </Button>

                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={
                        disabled ||
                        !hasNextPage
                    }
                    onClick={onNext}
                >
                    Next
                </Button>
            </div>
        </footer>
    );
}

type ProjectsListEmptyStateProps = {
    status?: ProjectStatus;
};

function ProjectsListEmptyState({
    status,
}: ProjectsListEmptyStateProps) {
    const hasStatusFilter =
        Boolean(status);

    return (
        <section className="mt-6 rounded-2xl border border-dashed bg-card/50 p-10 text-center">
            <div className="mx-auto flex size-14 items-center justify-center rounded-2xl border border-primary/30 bg-primary/10">
                <FolderKanban className="size-6 text-electric" />
            </div>

            <h2 className="mt-5 text-lg font-semibold">
                {hasStatusFilter
                    ? `No ${formatProjectStatus(status!)} projects`
                    : "No projects yet"}
            </h2>

            <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">
                {hasStatusFilter
                    ? "No projects match the selected status. Try another filter."
                    : "Create your first project and start receiving proposals from freelancers."}
            </p>

            {!hasStatusFilter && (
                <Button
                    asChild
                    className="mt-6"
                >
                    <Link href="/dashboard/client/projects/new">
                        Create first project
                    </Link>
                </Button>
            )}
        </section>
    );
}
type ProjectsListErrorProps = {
    error: unknown;
    isRetrying: boolean;
    onRetry: () => void;
};

function ProjectsListError({
    error,
    isRetrying,
    onRetry,
}: ProjectsListErrorProps) {
    return (
        <section className="mt-6 rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
            <div className="flex size-11 items-center justify-center rounded-xl bg-destructive/10">
                <AlertCircle className="size-5 text-destructive" />
            </div>

            <h2 className="mt-4 font-semibold">
                Could not load projects
            </h2>

            <p className="mt-2 max-w-xl text-sm text-muted-foreground">
                {getApiErrorMessage(
                    error,
                    "Project list could not be loaded.",
                )}
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

function formatNumber(
    value: number,
) {
    return new Intl.NumberFormat(
        "en-US",
    ).format(value);
}