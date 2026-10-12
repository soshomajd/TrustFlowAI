"use client";
import { MemberName } from "@/features/profile-photos/components/user-avatar";


import {
    AlertTriangle,
    ArrowLeft,
    CalendarDays,
    CheckCircle2,
    CircleDollarSign,
    Clock3,
    LoaderCircle,
    Play,
    RefreshCw,
    UserRound,
} from "lucide-react";
import Link from "next/link";

import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from "@/components/motion/animation-primitives";
import { Button } from
    "@/components/ui/button";
import { EscrowPanel } from
    "@/features/escrow/components/escrow-panel";
import { useStartMilestone } from
    "@/features/milestones/hooks/use-start-milestone";
import { SubmitMilestoneDialog } from
    "@/features/milestones/components/submit-milestone-dialog";
import { EscrowCasePanel } from
    "@/features/escrow/components/escrow-case-panel";
import { DisputePanel } from
    "@/features/ai/components/dispute-panel";
import { MilestoneSubmissionsPanel } from
    "@/features/milestones/components/milestone-submissions-panel";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";
import { useProjectEscrows } from
    "@/features/escrow/hooks/use-project-escrows";
import { useProjectWorkspace } from
    "@/features/projects/hooks/use-project-workspace";
import { useAuthStore } from "@/stores/auth-store";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";

type FreelancerProjectWorkspaceScreenProps = {
    projectId: string;
};

export function FreelancerProjectWorkspaceScreen({
    projectId,
}: FreelancerProjectWorkspaceScreenProps) {
    const workspaceQuery =
        useProjectWorkspace(projectId);

    const viewerId = useAuthStore((state) => state.user?.id ?? null);

    // A project can be split between freelancers, and each one is paid by
    // their own escrow. Work can only start once that escrow is funded.
    const escrowsQuery = useProjectEscrows(projectId);

    if (workspaceQuery.isPending) {
        return (
            <FreelancerWorkspaceSkeleton />
        );
    }

    if (workspaceQuery.isError) {
        return (
            <WorkspaceError
                message={getApiErrorMessage(
                    workspaceQuery.error,
                    "Project workspace could not be loaded.",
                )}
                isRetrying={
                    workspaceQuery.isFetching
                }
                onRetry={() => {
                    void workspaceQuery.refetch();
                }}
            />
        );
    }

    const workspace =
        workspaceQuery.data;

    // Other freelancers may hold the remaining stages; those are not this
    // freelancer's to see or act on.
    const milestones = workspace.milestones
        .filter((milestone) => milestone.freelancerId === viewerId)
        .sort(
            (firstMilestone, secondMilestone) =>
                firstMilestone.sequenceNumber -
                secondMilestone.sequenceNumber,
        );

    const myEscrow =
        escrowsQuery.data?.find(
            (participant) => participant.freelancerId === viewerId,
        )?.escrow ?? null;

    const isEscrowFunded = myEscrow?.status === "Funded";

    const approvedCount =
        milestones.filter(
            (milestone) =>
                milestone.status === "Approved" ||
                milestone.status === "Settled",
        ).length;

    const progress =
        milestones.length === 0
            ? 0
            : Math.round(
                (approvedCount /
                    milestones.length) *
                100,
            );

    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <Button
                asChild
                variant="ghost"
                size="sm"
            >
                <Link href="/dashboard/freelancer/projects">
                    <ArrowLeft className="size-4" />
                    Assigned projects
                </Link>
            </Button>

            <AnimatedSection className="mt-5">
                <section className="rounded-2xl border bg-card/70 p-6 backdrop-blur sm:p-8">
                    <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
                        <div className="min-w-0">
                            <div className="flex flex-wrap items-center gap-3">
                                <h1 className="text-3xl font-semibold">
                                    {workspace.title}
                                </h1>

                                <ProjectStatusBadge
                                    status={workspace.status}
                                />
                            </div>

                            <p className="mt-4 max-w-3xl leading-7 text-muted-foreground">
                                {workspace.description}
                            </p>

                            <div className="mt-5 flex flex-wrap gap-4 text-sm text-muted-foreground">
                                <span className="flex items-center gap-2">
                                    <UserRound className="size-4" />
                                    Client:{" "}
                                    {workspace.clientId ? <Link className="font-medium text-foreground hover:text-electric hover:underline" href={`/dashboard/clients/${workspace.clientId}`}><MemberName userId={workspace.clientId} name={workspace.clientFullName} /></Link> : workspace.clientFullName}
                                </span>

                                <span className="flex items-center gap-2">
                                    <CalendarDays className="size-4" />
                                    Deadline:{" "}
                                    {formatDate(
                                        workspace.deadline,
                                    )}
                                </span>
                            </div>
                        </div>

                        <div className="grid shrink-0 gap-3 sm:grid-cols-2 lg:min-w-72 lg:grid-cols-1">
                            <WorkspaceMeta
                                icon={CircleDollarSign}
                                label="Project budget"
                                value={formatAmount(
                                    workspace.budget,
                                )}
                            />

                            <WorkspaceMeta
                                icon={CheckCircle2}
                                label="Milestone progress"
                                value={`${approvedCount} of ${milestones.length} approved`}
                            />
                        </div>
                    </div>

                    <div className="mt-6">
                        <div className="mb-2 flex items-center justify-between text-xs">
                            <span className="text-muted-foreground">
                                Project completion
                            </span>

                            <span className="font-semibold">
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
                </section>
            </AnimatedSection>

            <AnimatedSection className="mt-6">
                <EscrowPanel
                    projectId={workspace.id}
                    projectStatus={workspace.status}
                    viewerRole="Freelancer"
                />
            </AnimatedSection>

            <AnimatedSection className="mt-6">
                <section className="rounded-2xl border bg-card/60 p-5 backdrop-blur sm:p-6">
                    <div>
                        <h2 className="text-xl font-semibold">
                            Project milestones
                        </h2>

                        <p className="mt-2 text-sm text-muted-foreground">
                            Complete milestones in
                            sequence and submit finished
                            work for client review.
                        </p>
                    </div>

                    {milestones.length === 0 ? (
                        <div className="mt-6 rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">
                            No milestones have been
                            created for this project.
                        </div>
                    ) : (
                        <StaggerContainer className="mt-6 space-y-4">
                            {milestones.map(
                                (milestone, position) => (
                                    <StaggerItem
                                        key={milestone.id}
                                    >
                                        <FreelancerMilestoneCard
                                            escrow={myEscrow}
                                            onChainIndex={position}
                                            isNextInLine={milestones
                                                .slice(0, position)
                                                .every(
                                                    (earlier) =>
                                                        earlier.status ===
                                                            "Approved" ||
                                                        earlier.status ===
                                                            "Settled",
                                                )}
                                            projectId={
                                                workspace.id
                                            }
                                            projectStatus={
                                                workspace.status
                                            }
                                            milestone={
                                                milestone
                                            }
                                            milestones={
                                                milestones
                                            }
                                            isEscrowFunded={
                                                isEscrowFunded
                                            }
                                        />
                                    </StaggerItem>
                                ),
                            )}
                        </StaggerContainer>
                    )}
                </section>
            </AnimatedSection>
        </main>
    );
}

type WorkspaceQuery =
    ReturnType<
        typeof useProjectWorkspace
    >;

type WorkspaceData =
    NonNullable<
        WorkspaceQuery["data"]
    >;

type WorkspaceMilestone =
    WorkspaceData["milestones"][number];

function FreelancerMilestoneCard({
    projectId,
    projectStatus,
    milestone,
    milestones,
    isEscrowFunded,
    escrow,
    onChainIndex,
    isNextInLine,
}: {
    escrow: ProjectEscrow | null;
    onChainIndex: number;
    isNextInLine: boolean;
    projectId: string;
    projectStatus:
    WorkspaceData["status"];
    milestone: WorkspaceMilestone;
    milestones: WorkspaceMilestone[];
    isEscrowFunded: boolean;
}) {
    const startMutation =
        useStartMilestone(
            projectId,
            milestone.id,
        );

    const isRejected =
        milestone.status === "Rejected";

    const blockingMilestone =
        milestones.find(
            (previousMilestone) =>
                previousMilestone.sequenceNumber <
                milestone.sequenceNumber &&
                previousMilestone.status !==
                "Approved" &&
                previousMilestone.status !==
                "Settled",
        );

    const canStart =
        projectStatus === "InProgress" &&
        isEscrowFunded &&
        (
            milestone.status === "Pending" ||
            milestone.status === "Rejected"
        ) &&
        !blockingMilestone;

    const canSubmit =
        projectStatus === "InProgress" &&
        milestone.status === "InProgress";

    const isMutating =
        startMutation.isPending;

    const mutationError =
        startMutation.isError
            ? getApiErrorMessage(
                startMutation.error,
                "Milestone could not be started.",
            )
            : null;

    return (
        <article
            className={cn(
                "rounded-xl border bg-background/30 p-5 transition",
                isRejected &&
                "border-red-500/40 bg-red-500/6 shadow-[inset_12px_0_30px_-24px_rgba(239,68,68,0.9)]",
            )}
        >
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-3">
                        <span className="flex size-8 items-center justify-center rounded-full border bg-card text-xs font-semibold">
                            {milestone.sequenceNumber}
                        </span>

                        <h3 className="font-semibold">
                            {milestone.title}
                        </h3>

                        <MilestoneStatusBadge
                            status={milestone.status}
                        />
                    </div>

                    <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                        {milestone.description}
                    </p>
                </div>

                <div className="grid shrink-0 gap-3 sm:grid-cols-2 lg:min-w-64 lg:grid-cols-1">
                    <WorkspaceMeta
                        icon={CircleDollarSign}
                        label="Amount"
                        value={formatAmount(
                            milestone.amount,
                        )}
                    />

                    <WorkspaceMeta
                        icon={Clock3}
                        label="Deadline"
                        value={formatDate(
                            milestone.deadline,
                        )}
                    />
                </div>
            </div>

            {isRejected && (
                <div className="mt-5 flex gap-3 rounded-xl border border-red-500/30 bg-red-500/10 p-4 text-sm text-red-200">
                    <AlertTriangle className="mt-0.5 size-4 shrink-0 text-red-400" />

                    <p>
                        The client requested changes.
                        Restart this milestone, make the
                        required updates, and submit it
                        again.
                    </p>
                </div>
            )}

            {blockingMilestone &&
                (
                    milestone.status === "Pending" ||
                    milestone.status === "Rejected"
                ) && (
                    <div className="mt-5 flex gap-3 rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 text-sm text-muted-foreground">
                        <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-400" />

                        <p>
                            Milestone{" "}
                            {
                                blockingMilestone.sequenceNumber
                            }{" "}
                            must be approved before
                            this milestone can be
                            started.
                        </p>
                    </div>
                )}

            {!isEscrowFunded &&
                (
                    milestone.status === "Pending" ||
                    milestone.status === "Rejected"
                ) && (
                    <div className="mt-5 flex gap-3 rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 text-sm text-muted-foreground">
                        <AlertTriangle className="mt-0.5 size-4 shrink-0 text-amber-400" />

                        <p>
                            Do not start yet. Your escrow must say Funded
                            before your payment is protected, and the client
                            has not funded it yet.
                        </p>
                    </div>
                )}

            {milestone.status ===
                "Submitted" && (
                    <div className="mt-5 rounded-xl border border-blue-500/20 bg-blue-500/5 p-4 text-sm text-muted-foreground">
                        This milestone has been
                        submitted and is waiting for
                        client review.
                    </div>
                )}

            {milestone.status ===
                "Approved" && (
                    <div className="mt-5 rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-4 text-sm text-muted-foreground">
                        This milestone has been
                        approved by the client.
                    </div>
                )}

            {milestone.status ===
                "Settled" && (
                    <div className="mt-5 rounded-xl border border-blue-500/20 bg-blue-500/5 p-4 text-sm text-muted-foreground">
                        A reviewer settled this
                        milestone. You received{" "}
                        {formatSettledAmount(
                            milestone.paidAmount ?? 0,
                        )}{" "}
                        of{" "}
                        {formatSettledAmount(
                            milestone.amount,
                        )}
                        .
                    </div>
                )}

            {milestone.status !== "Pending" && (
                <MilestoneSubmissionsPanel
                    projectId={projectId}
                    milestoneId={milestone.id}
                />
            )}

            {milestone.status !== "Pending" &&
                milestone.status !== "InProgress" && (
                    <DisputePanel
                        projectId={projectId}
                        milestoneId={milestone.id}
                        milestoneStatus={milestone.status}
                        canOpen={isEscrowFunded}
                    />
                )}

            <EscrowCasePanel
                projectId={projectId}
                viewerRole="Freelancer"
                escrow={escrow}
                milestone={milestone}
                onChainIndex={onChainIndex}
                isNextInLine={isNextInLine}
            />

            {mutationError && (
                <div
                    role="alert"
                    className="mt-5 flex gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4"
                >
                    <AlertTriangle className="mt-0.5 size-5 shrink-0 text-destructive" />

                    <p className="text-sm text-destructive">
                        {mutationError}
                    </p>
                </div>
            )}

            {(canStart || canSubmit) && (
                <div className="mt-5 flex justify-end border-t pt-5">
                    {canStart && (
                        <Button
                            type="button"
                            variant={
                                isRejected
                                    ? "outline"
                                    : "default"
                            }
                            className={cn(
                                isRejected &&
                                "border-red-500/30 bg-red-500/10 text-red-200 hover:bg-red-500/20 hover:text-red-100",
                            )}
                            disabled={isMutating}
                            onClick={() => {
                                startMutation.reset();
                                startMutation.mutate();
                            }}
                        >
                            {startMutation.isPending ? (
                                <LoaderCircle className="size-4 animate-spin" />
                            ) : (
                                <Play className="size-4" />
                            )}

                            {milestone.status ===
                                "Rejected"
                                ? "Restart milestone"
                                : "Start milestone"}
                        </Button>
                    )}

                    {canSubmit && (
                        <SubmitMilestoneDialog
                            projectId={projectId}
                            milestoneId={milestone.id}
                        />
                    )}
                </div>
            )}
        </article>
    );
}

function WorkspaceMeta({
    icon: Icon,
    label,
    value,
}: {
    icon: typeof CalendarDays;
    label: string;
    value: React.ReactNode;
}) {
    return (
        <div className="rounded-xl border bg-card/40 px-4 py-3">
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
    status: WorkspaceData["status"];
}) {
    return (
        <span
            className={cn(
                "rounded-full border px-2.5 py-1 text-xs font-semibold",
                status === "InProgress" &&
                "border-blue-500/30 bg-blue-500/10 text-blue-400",
                status === "Completed" &&
                "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",
                status === "Open" &&
                "border-cyan-500/30 bg-cyan-500/10 text-cyan-400",
                status === "Cancelled" &&
                "border-red-500/30 bg-red-500/10 text-red-400",
            )}
        >
            {status === "InProgress"
                ? "In progress"
                : status}
        </span>
    );
}

function MilestoneStatusBadge({
    status,
}: {
    status:
    WorkspaceMilestone["status"];
}) {
    return (
        <span
            className={cn(
                "rounded-full border px-2.5 py-1 text-xs font-medium",

                status === "Pending" &&
                "border-slate-500/30 bg-slate-500/10 text-slate-300",

                status === "InProgress" &&
                "border-blue-500/30 bg-blue-500/10 text-blue-400",

                status === "Submitted" &&
                "border-violet-500/30 bg-violet-500/10 text-violet-400",

                status === "Approved" &&
                "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",

                status === "Rejected" &&
                "border-red-500/30 bg-red-500/10 text-red-400",

                status === "Settled" &&
                "border-blue-500/30 bg-blue-500/10 text-blue-300",
            )}
        >
            {status === "InProgress"
                ? "In progress"
                : status}
        </span>
    );
}

function formatSettledAmount(value: number) {
    return new Intl.NumberFormat("en-US", {
        style: "currency",
        currency: "USD",
    }).format(value);
}

function WorkspaceError({
    message,
    isRetrying,
    onRetry,
}: {
    message: string;
    isRetrying: boolean;
    onRetry: () => void;
}) {
    return (
        <main className="mx-auto max-w-5xl px-4 py-10">
            <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
                <AlertTriangle className="size-6 text-destructive" />

                <h1 className="mt-4 text-lg font-semibold">
                    Could not load workspace
                </h1>

                <p className="mt-2 text-sm text-muted-foreground">
                    {message}
                </p>

                <Button
                    type="button"
                    variant="outline"
                    className="mt-5"
                    disabled={isRetrying}
                    onClick={onRetry}
                >
                    {isRetrying ? (
                        <LoaderCircle className="size-4 animate-spin" />
                    ) : (
                        <RefreshCw className="size-4" />
                    )}

                    Try again
                </Button>
            </section>
        </main>
    );
}

export function FreelancerWorkspaceSkeleton() {
    return (
        <main className="mx-auto max-w-7xl animate-pulse px-4 py-8 sm:px-6">
            <div className="h-64 rounded-2xl border bg-muted/30" />

            <div className="mt-6 h-96 rounded-2xl border bg-muted/20" />
        </main>
    );
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
