"use client";
import { sumMoney } from "@/lib/money/exact-money";
import { formatMoney as formatAmount, moneyText } from "@/lib/money/exact-money";
import { MessageButton } from "@/features/chat/components/message-button";
import { MemberName } from "@/features/profile-photos/components/user-avatar";


import { isAxiosError } from "axios";
import {
    ArrowLeft,
    CalendarDays,
    CircleDollarSign,
    FolderKanban,
    LoaderCircle,
    RefreshCw,
    TriangleAlert,
    UserRound,
} from "lucide-react";
import Link from "next/link";

import {
    AnimatedSection,
} from "@/components/motion/animation-primitives";
import { Button } from
    "@/components/ui/button";
import { useProjectWorkspace } from
    "@/features/projects/hooks/use-project-workspace";
import type { ProjectStatus } from
    "@/features/projects/types/project";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { isValidGuid } from
    "@/lib/validation/is-valid-guid";
import { cn } from "@/lib/utils";

type ProjectWorkspaceScreenProps = {
    projectId: string;
};
import { ProjectMilestonesSection } from
    "@/features/milestones/components/project-milestones-section";
import { ProjectProposalsSection } from
    "@/features/proposals/components/project-proposals-section";
import { EscrowPanel } from
    "@/features/escrow/components/escrow-panel";
import { ProjectHiddenNotice } from
    "@/features/projects/components/project-hidden-notice";

export function ProjectWorkspaceScreen({
    projectId,
}: ProjectWorkspaceScreenProps) {
    const normalizedProjectId =
        projectId.trim();

    const workspaceQuery =
        useProjectWorkspace(
            normalizedProjectId,
        );

    if (
        !isValidGuid(
            normalizedProjectId,
        )
    ) {
        return (
            <ProjectWorkspaceInvalidId />
        );
    }

    if (workspaceQuery.isLoading) {
        return (
            <ProjectWorkspaceSkeleton />
        );
    }

    if (workspaceQuery.isError) {
        const isNotFound =
            isAxiosError(
                workspaceQuery.error,
            ) &&
            workspaceQuery.error.response
                ?.status === 404;

        if (isNotFound) {
            return (
                <ProjectWorkspaceNotFound />
            );
        }

        return (
            <ProjectWorkspaceError
                message={getApiErrorMessage(
                    workspaceQuery.error,
                    "Project workspace could not be loaded.",
                )}
                onRetry={() => {
                    void workspaceQuery.refetch();
                }}
            />
        );
    }

    if (!workspaceQuery.data) {
        return null;
    }

    const project =
        workspaceQuery.data;

    const allocatedAmount = sumMoney(project.milestones.map((milestone) => moneyText(milestone, "amount")));

    // Work is assigned per milestone, so a project can have several
    // freelancers. Group the stages by owner to list them.
    const assignedFreelancers = [
        ...project.milestones
            .reduce((owners, milestone) => {
                if (!milestone.freelancerId) {
                    return owners;
                }

                const existing = owners.get(milestone.freelancerId);

                owners.set(milestone.freelancerId, {
                    id: milestone.freelancerId,
                    fullName:
                        milestone.freelancerFullName ??
                        existing?.fullName ??
                        null,
                    milestoneCount: (existing?.milestoneCount ?? 0) + 1,
                    total: sumMoney([existing?.total ?? "0", moneyText(milestone, "amount")]),
                });

                return owners;
            }, new Map())
            .values(),
    ].sort((left, right) =>
        (left.fullName ?? "").localeCompare(right.fullName ?? ""),
    );

    return (
        <AnimatedSection className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
            <header className="mb-8">
                <Button
                    asChild
                    variant="ghost"
                    size="sm"
                    className="-ml-3 mb-5"
                >
                    <Link href="/dashboard/client/projects">
                        <ArrowLeft className="size-4" />
                        Back to projects
                    </Link>
                </Button>

                <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0">
                        <div className="mb-3 flex flex-wrap items-center gap-3">
                            <div className="flex items-center gap-2 text-sm font-medium uppercase tracking-[0.2em] text-electric">
                                <FolderKanban className="size-4" />
                                Project workspace
                            </div>

                            <ProjectStatusBadge
                                status={project.status}
                            />
                        </div>

                        <h1 className="wrap-break-word text-3xl font-semibold tracking-tight sm:text-4xl">
                            {project.title}
                        </h1>

                        <p className="mt-3 max-w-3xl whitespace-pre-wrap leading-7 text-muted-foreground">
                            {project.description}
                        </p>
                    </div>

                    {workspaceQuery.isFetching && (
                        <div className="flex shrink-0 items-center gap-2 text-sm text-muted-foreground">
                            <LoaderCircle className="size-4 animate-spin" />
                            Updating workspace
                        </div>
                    )}
                </div>
            </header>

            <ProjectHiddenNotice
                status={project.status}
                budget={moneyText(project, "budget")}
                allocatedAmount={allocatedAmount}
                milestoneCount={project.milestones.length}
            />

            <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <WorkspaceDetailCard
                    icon={CircleDollarSign}
                    label="Budget"
                    value={formatAmount(moneyText(project, "budget"))}
                />

                <WorkspaceDetailCard
                    icon={CalendarDays}
                    label="Deadline"
                    value={formatDate(
                        project.deadline,
                    )}
                />

                <WorkspaceDetailCard
                    icon={UserRound}
                    label="Client"
                    value={<MemberName userId={project.clientId} name={project.clientFullName} />}
                />

                <WorkspaceDetailCard
                    icon={CalendarDays}
                    label="Created"
                    value={formatDate(
                        project.createdAt,
                    )}
                />
            </div>

            <div className="mt-6 rounded-2xl border bg-card/70 p-5 backdrop-blur sm:p-6">
                <p className="text-sm font-medium text-muted-foreground">
                    {assignedFreelancers.length === 1
                        ? "Assigned freelancer"
                        : "Assigned freelancers"}
                </p>

                {assignedFreelancers.length === 0 ? (
                    <p className="mt-2 font-medium">
                        No freelancer assigned yet
                    </p>
                ) : (
                    <ul className="mt-3 space-y-3">
                        {assignedFreelancers.map((freelancer) => (
                            <li
                                key={freelancer.id}
                                className="flex flex-wrap items-center justify-between gap-3"
                            >
                                <div className="min-w-0">
                                    <p className="font-medium">
                                        <MemberName
                                            userId={freelancer.id}
                                            name={freelancer.fullName}
                                        />
                                    </p>

                                    <p className="mt-1 text-sm text-muted-foreground">
                                        {freelancer.milestoneCount === 1
                                            ? "1 stage"
                                            : `${freelancer.milestoneCount} stages`}
                                        {" · "}
                                        {formatAmount(freelancer.total)}
                                    </p>
                                </div>

                                <div className="flex flex-wrap gap-2">
                                    <MessageButton
                                        projectId={project.id}
                                        freelancerId={freelancer.id}
                                    />

                                    <Button asChild variant="outline" size="sm">
                                        <Link href={`/dashboard/freelancers/${freelancer.id}`}>
                                            <UserRound className="size-4" />View profile
                                        </Link>
                                    </Button>
                                </div>
                            </li>
                        ))}
                    </ul>
                )}
            </div>
            <div className="mt-6">
                <EscrowPanel
                    projectId={project.id}
                    projectStatus={project.status}
                    viewerRole="Client"
                />
            </div>
            <div className="mt-6">
                <ProjectMilestonesSection
                    projectId={project.id}
                    hasProposals={
                        project.activeProposalCount > 0
                    }
                    projectBudget={moneyText(project, "budget")}
                    projectDeadline={
                        project.deadline
                    }
                    projectStatus={
                        project.status
                    }
                />
            </div>
            <div className="mt-6">
                <ProjectProposalsSection
                    projectId={project.id}
                    projectBudget={moneyText(project, "budget")}
                />
            </div>
        </AnimatedSection>
    );
}

type WorkspaceDetailCardProps = {
    icon: typeof FolderKanban;
    label: string;
    value: React.ReactNode;
};

function WorkspaceDetailCard({
    icon: Icon,
    label,
    value,
}: WorkspaceDetailCardProps) {
    return (
        <article className="rounded-2xl border bg-card/70 p-5 backdrop-blur">
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Icon className="size-4" />
                {label}
            </div>

            <p className="mt-3 truncate text-lg font-semibold">
                {value}
            </p>
        </article>
    );
}

function ProjectStatusBadge({
    status,
}: {
    status: ProjectStatus;
}) {
    const className =
        statusStyles[status];

    return (
        <span
            className={cn(
                "rounded-full border px-2.5 py-1 text-xs font-medium",
                className,
            )}
        >
            {formatLabel(status)}
        </span>
    );
}

const statusStyles: Record<
    ProjectStatus,
    string
> = {
    Open:
        "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",

    InProgress:
        "border-blue-500/30 bg-blue-500/10 text-blue-400",

    Completed:
        "border-violet-500/30 bg-violet-500/10 text-violet-400",

    Cancelled:
        "border-red-500/30 bg-red-500/10 text-red-400",
};

function ProjectWorkspaceSkeleton() {
    return (
        <main className="mx-auto max-w-6xl animate-pulse px-4 py-8 sm:px-6">
            <div className="h-9 w-36 rounded-md bg-muted" />

            <div className="mt-8 h-10 max-w-xl rounded-md bg-muted" />

            <div className="mt-4 h-24 max-w-3xl rounded-md bg-muted" />

            <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {Array.from({
                    length: 4,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="h-28 rounded-2xl border bg-muted/50"
                    />
                ))}
            </div>

            <div className="mt-6 h-28 rounded-2xl border bg-muted/50" />

            <div className="mt-6 h-72 rounded-2xl border bg-muted/50" />
        </main>
    );
}

function ProjectWorkspaceInvalidId() {
    return (
        <ProjectWorkspaceState
            title="Invalid project address"
            description="The project ID in this URL is not valid."
        />
    );
}

function ProjectWorkspaceNotFound() {
    return (
        <ProjectWorkspaceState
            title="Project workspace not found"
            description="This project does not exist, or your account does not have access to it."
        />
    );
}

function ProjectWorkspaceError({
    message,
    onRetry,
}: {
    message: string;
    onRetry: () => void;
}) {
    return (
        <ProjectWorkspaceState
            title="Could not load workspace"
            description={message}
            action={
                <Button
                    type="button"
                    onClick={onRetry}
                >
                    <RefreshCw className="size-4" />
                    Try again
                </Button>
            }
        />
    );
}

function ProjectWorkspaceState({
    title,
    description,
    action,
}: {
    title: string;
    description: string;
    action?: React.ReactNode;
}) {
    return (
        <main className="mx-auto flex min-h-[60vh] max-w-3xl items-center justify-center px-4 py-12">
            <div className="w-full rounded-2xl border bg-card/70 p-8 text-center backdrop-blur">
                <TriangleAlert className="mx-auto size-10 text-muted-foreground" />

                <h1 className="mt-4 text-xl font-semibold">
                    {title}
                </h1>

                <p className="mx-auto mt-2 max-w-lg text-sm leading-6 text-muted-foreground">
                    {description}
                </p>

                <div className="mt-6 flex flex-wrap justify-center gap-3">
                    {action}

                    <Button
                        asChild
                        variant="outline"
                    >
                        <Link href="/dashboard/client/projects">
                            <ArrowLeft className="size-4" />
                            Back to projects
                        </Link>
                    </Button>
                </div>
            </div>
        </main>
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

function formatLabel(
    value: string,
) {
    return value.replace(
        /([a-z])([A-Z])/g,
        "$1 $2",
    );
}
