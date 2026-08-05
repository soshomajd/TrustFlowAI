"use client";

import type { LucideIcon } from
    "lucide-react";
import {
    ArrowLeft,
    BriefcaseBusiness,
    CalendarDays,
    CircleDollarSign,
    Clock3,
    Layers3,
    RefreshCw,
    TriangleAlert,
} from "lucide-react";
import Link from "next/link";

import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from
    "@/components/motion/animation-primitives";
import { Button } from
    "@/components/ui/button";
import { SubmitProposalDialog } from
    "@/features/proposals/components/submit-proposal-dialog";
import { useMarketplaceProjectDetails } from
    "@/features/projects/hooks/use-marketplace-project-details";
import type {
    PublicMarketplaceMilestone,
} from
    "@/features/projects/types/marketplace-project-details";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { isValidGuid } from
    "@/lib/validation/is-valid-guid";

type MarketplaceProjectDetailsScreenProps = {
    projectId: string;
};

export function MarketplaceProjectDetailsScreen({
    projectId,
}: MarketplaceProjectDetailsScreenProps) {
    const normalizedProjectId =
        projectId.trim();

    const isProjectIdValid =
        isValidGuid(
            normalizedProjectId,
        );

    const projectQuery =
        useMarketplaceProjectDetails(
            normalizedProjectId,
        );

    if (!isProjectIdValid) {
        return (
            <MarketplaceProjectNotFound
                message="The project identifier is invalid."
            />
        );
    }

    if (projectQuery.isLoading) {
        return (
            <MarketplaceProjectDetailsSkeleton />
        );
    }

    if (
        projectQuery.isError ||
        !projectQuery.data
    ) {
        return (
            <MarketplaceProjectError
                message={getApiErrorMessage(
                    projectQuery.error,
                    "The project could not be loaded.",
                )}
                onRetry={() => {
                    void projectQuery.refetch();
                }}
            />
        );
    }

    const project =
        projectQuery.data;

    const remainingBudget =
        Math.max(
            0,
            project.budget -
            project.allocatedAmount,
        );

    const milestones = [
        ...project.milestones,
    ].sort(
        (firstMilestone, secondMilestone) =>
            firstMilestone.sequenceNumber -
            secondMilestone.sequenceNumber,
    );

    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <Button
                asChild
                variant="ghost"
                className="-ml-3 mb-5"
            >
                <Link href="/dashboard/freelancer/marketplace">
                    <ArrowLeft className="size-4" />
                    Back to marketplace
                </Link>
            </Button>

            <AnimatedSection>
                <section className="rounded-2xl border bg-card/80 p-6 shadow-blue-glow backdrop-blur sm:p-8">
                    <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
                        <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-3">
                                <div className="flex size-12 items-center justify-center rounded-xl bg-primary/10">
                                    <BriefcaseBusiness className="size-6 text-electric" />
                                </div>

                                <span className="rounded-full border border-emerald-500/30 bg-emerald-500/10 px-3 py-1 text-xs font-medium text-emerald-400">
                                    {project.status}
                                </span>
                            </div>

                            <p className="mt-6 text-sm font-medium uppercase tracking-[0.22em] text-electric">
                                Marketplace project
                            </p>

                            <h1 className="mt-3 text-3xl font-semibold sm:text-4xl">
                                {project.title}
                            </h1>

                            <p className="mt-4 max-w-3xl whitespace-pre-wrap text-sm leading-7 text-muted-foreground sm:text-base">
                                {project.description}
                            </p>
                        </div>

                        <div className="shrink-0 lg:w-64">
                            <SubmitProposalDialog
                                projectId={project.id}
                                projectTitle={
                                    project.title
                                }
                                projectBudget={
                                    project.budget
                                }
                                disabled={
                                    project.status !==
                                    "Open"
                                }
                                disabledReason="Proposals can only be submitted for open projects."
                            />
                        </div>
                    </div>
                </section>
            </AnimatedSection>

            <AnimatedSection
                className="mt-6"
            >
                <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                    <ProjectSummaryCard
                        icon={CircleDollarSign}
                        label="Project budget"
                        value={formatAmount(
                            project.budget,
                        )}
                    />

                    <ProjectSummaryCard
                        icon={CircleDollarSign}
                        label="Available budget"
                        value={formatAmount(
                            remainingBudget,
                        )}
                    />

                    <ProjectSummaryCard
                        icon={Layers3}
                        label="Milestones"
                        value={String(
                            milestones.length,
                        )}
                    />

                    <ProjectSummaryCard
                        icon={CalendarDays}
                        label="Deadline"
                        value={formatDate(
                            project.deadline,
                        )}
                    />
                </section>
            </AnimatedSection>

            <AnimatedSection
                className="mt-6"
            >
                <section
                    aria-labelledby="public-milestones-title"
                    className="rounded-2xl border bg-card/60 p-5 backdrop-blur sm:p-6"
                >
                    <header>
                        <div className="flex items-center gap-2">
                            <Layers3 className="size-5 text-electric" />

                            <h2
                                id="public-milestones-title"
                                className="text-xl font-semibold"
                            >
                                Milestone plan
                            </h2>
                        </div>

                        <p className="mt-2 text-sm text-muted-foreground">
                            Review the planned work,
                            payment amounts and
                            delivery deadlines before
                            submitting your proposal.
                        </p>
                    </header>

                    {milestones.length === 0 ? (
                        <div className="mt-6 rounded-xl border border-dashed p-8 text-center">
                            <Layers3 className="mx-auto size-8 text-muted-foreground" />

                            <p className="mt-3 font-medium">
                                No milestones defined
                            </p>

                            <p className="mt-1 text-sm text-muted-foreground">
                                The client has not added
                                a milestone plan yet.
                            </p>
                        </div>
                    ) : (
                        <StaggerContainer className="mt-6 space-y-3">
                            {milestones.map(
                                (milestone) => (
                                    <StaggerItem
                                        key={milestone.id}
                                    >
                                        <PublicMilestoneCard
                                            milestone={
                                                milestone
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

type ProjectSummaryCardProps = {
    icon: LucideIcon;
    label: string;
    value: string;
};

function ProjectSummaryCard({
    icon: Icon,
    label,
    value,
}: ProjectSummaryCardProps) {
    return (
        <article className="rounded-xl border bg-card/60 p-5">
            <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10">
                <Icon className="size-4 text-electric" />
            </div>

            <p className="mt-4 text-sm text-muted-foreground">
                {label}
            </p>

            <p className="mt-1 text-lg font-semibold">
                {value}
            </p>
        </article>
    );
}

function PublicMilestoneCard({
    milestone,
}: {
    milestone:
    PublicMarketplaceMilestone;
}) {
    return (
        <article className="rounded-xl border bg-background/40 p-5">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0 flex-1">
                    <p className="text-xs font-medium uppercase tracking-[0.18em] text-electric">
                        Milestone{" "}
                        {milestone.sequenceNumber}
                    </p>

                    <h3 className="mt-2 text-lg font-semibold">
                        {milestone.title}
                    </h3>

                    <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                        {milestone.description}
                    </p>
                </div>

                <div className="grid shrink-0 gap-3 sm:min-w-52">
                    <MilestoneMeta
                        icon={CircleDollarSign}
                        label="Amount"
                        value={formatAmount(
                            milestone.amount,
                        )}
                    />

                    <MilestoneMeta
                        icon={Clock3}
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

function MilestoneMeta({
    icon: Icon,
    label,
    value,
}: ProjectSummaryCardProps) {
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

function MarketplaceProjectError({
    message,
    onRetry,
}: {
    message: string;
    onRetry: () => void;
}) {
    return (
        <main className="mx-auto max-w-4xl px-4 py-8 sm:px-6">
            <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex gap-3">
                        <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                        <div>
                            <h1 className="font-semibold">
                                Could not load project
                            </h1>

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

                <Button
                    asChild
                    variant="ghost"
                    className="mt-5"
                >
                    <Link href="/dashboard/freelancer/marketplace">
                        <ArrowLeft className="size-4" />
                        Back to marketplace
                    </Link>
                </Button>
            </section>
        </main>
    );
}

function MarketplaceProjectNotFound({
    message,
}: {
    message: string;
}) {
    return (
        <main className="mx-auto max-w-4xl px-4 py-8 sm:px-6">
            <section className="rounded-2xl border border-dashed p-8 text-center">
                <BriefcaseBusiness className="mx-auto size-10 text-muted-foreground" />

                <h1 className="mt-4 text-xl font-semibold">
                    Project not found
                </h1>

                <p className="mt-2 text-sm text-muted-foreground">
                    {message}
                </p>

                <Button
                    asChild
                    className="mt-6"
                >
                    <Link href="/dashboard/freelancer/marketplace">
                        Back to marketplace
                    </Link>
                </Button>
            </section>
        </main>
    );
}

export function MarketplaceProjectDetailsSkeleton() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <div className="h-10 w-44 animate-pulse rounded bg-muted" />

            <div className="mt-5 h-80 animate-pulse rounded-2xl border bg-muted/30" />

            <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {Array.from({
                    length: 4,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="h-36 animate-pulse rounded-xl border bg-muted/30"
                    />
                ))}
            </div>

            <div className="mt-6 h-96 animate-pulse rounded-2xl border bg-muted/30" />
        </main>
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