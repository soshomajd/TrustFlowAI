"use client";

import {
    BriefcaseBusiness,
    ChevronLeft,
    ChevronRight,
    CircleDollarSign,
    Clock3,
    FileText,
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
} from
    "@/components/motion/animation-primitives";
import { Button } from
    "@/components/ui/button";
import { useProjectProposals } from
    "@/features/proposals/hooks/use-project-proposals";
import type {
    ClientProjectProposal,
    ProposalStatus,
} from
    "@/features/proposals/types/proposal";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";

type ProjectProposalsSectionProps = {
    projectId: string;
};

const PAGE_SIZE = 5;

export function ProjectProposalsSection({
    projectId,
}: ProjectProposalsSectionProps) {
    const [page, setPage] =
        useState(1);

    const proposalsQuery =
        useProjectProposals(
            projectId,
            {
                page,
                pageSize: PAGE_SIZE,
            },
        );

    if (proposalsQuery.isLoading) {
        return (
            <ProjectProposalsSkeleton />
        );
    }

    if (proposalsQuery.isError) {
        return (
            <ProjectProposalsError
                message={getApiErrorMessage(
                    proposalsQuery.error,
                    "Proposals could not be loaded.",
                )}
                onRetry={() => {
                    void proposalsQuery.refetch();
                }}
            />
        );
    }

    const proposals =
        proposalsQuery.data?.items ??
        [];

    const pagination =
        proposalsQuery.data;

    return (
        <AnimatedSection>
            <section
                aria-labelledby="project-proposals-title"
                className="rounded-2xl border bg-card/70 p-5 backdrop-blur sm:p-6"
            >
                <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                        <div className="flex items-center gap-2">
                            <BriefcaseBusiness className="size-5 text-electric" />

                            <h2
                                id="project-proposals-title"
                                className="text-lg font-semibold"
                            >
                                Proposals
                            </h2>
                        </div>

                        <p className="mt-2 text-sm text-muted-foreground">
                            Review bids submitted
                            by freelancers.
                        </p>
                    </div>

                    <div className="flex items-center gap-3">
                        <span className="rounded-full border px-3 py-1 text-xs font-medium text-muted-foreground">
                            {pagination?.totalItems ??
                                0}{" "}
                            {(pagination?.totalItems ??
                                0) === 1
                                ? "proposal"
                                : "proposals"}
                        </span>

                        {proposalsQuery.isFetching && (
                            <div className="flex items-center gap-2 text-xs text-muted-foreground">
                                <LoaderCircle className="size-3.5 animate-spin" />
                                Updating
                            </div>
                        )}
                    </div>
                </header>

                {proposals.length === 0 ? (
                    <ProjectProposalsEmpty />
                ) : (
                    <>
                        <StaggerContainer className="mt-6 space-y-3">
                            {proposals.map(
                                (proposal) => (
                                    <StaggerItem
                                        key={proposal.id}
                                    >
                                        <ProposalCard
                                            proposal={
                                                proposal
                                            }
                                        />
                                    </StaggerItem>
                                ),
                            )}
                        </StaggerContainer>

                        {pagination &&
                            pagination.totalPages >
                            1 && (
                                <ProposalPagination
                                    page={pagination.page}
                                    totalPages={
                                        pagination.totalPages
                                    }
                                    hasPreviousPage={
                                        pagination.hasPreviousPage
                                    }
                                    hasNextPage={
                                        pagination.hasNextPage
                                    }
                                    isFetching={
                                        proposalsQuery.isFetching
                                    }
                                    onPrevious={() => {
                                        setPage(
                                            (currentPage) =>
                                                Math.max(
                                                    1,
                                                    currentPage - 1,
                                                ),
                                        );
                                    }}
                                    onNext={() => {
                                        setPage(
                                            (currentPage) =>
                                                Math.min(
                                                    pagination.totalPages,
                                                    currentPage + 1,
                                                ),
                                        );
                                    }}
                                />
                            )}
                    </>
                )}
            </section>
        </AnimatedSection>
    );
}

function ProposalCard({
    proposal,
}: {
    proposal:
    ClientProjectProposal;
}) {
    return (
        <article className="rounded-xl border bg-background/40 p-4 transition hover:border-primary/40 hover:bg-background/70 sm:p-5">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-3">
                        <div className="flex items-center gap-2">
                            <UserRound className="size-4 text-muted-foreground" />

                            <h3 className="font-semibold">
                                {
                                    proposal.freelancerFullName
                                }
                            </h3>
                        </div>

                        <ProposalStatusBadge
                            status={
                                proposal.status
                            }
                        />
                    </div>

                    <div className="mt-4 flex items-start gap-2">
                        <FileText className="mt-1 size-4 shrink-0 text-muted-foreground" />

                        <p className="whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                            {proposal.coverLetter}
                        </p>
                    </div>

                    <p className="mt-4 text-xs text-muted-foreground">
                        Submitted{" "}
                        {formatDateTime(
                            proposal.createdAt,
                        )}
                    </p>
                </div>

                <div className="grid shrink-0 gap-3 sm:grid-cols-2 lg:min-w-60 lg:grid-cols-1">
                    <ProposalMeta
                        icon={
                            CircleDollarSign
                        }
                        label="Bid amount"
                        value={formatAmount(
                            proposal.bidAmount,
                        )}
                    />

                    <ProposalMeta
                        icon={Clock3}
                        label="Estimated time"
                        value={`${proposal.estimatedDays} ${proposal.estimatedDays ===
                                1
                                ? "day"
                                : "days"
                            }`}
                    />
                </div>
            </div>
        </article>
    );
}

type ProposalMetaProps = {
    icon: typeof Clock3;
    label: string;
    value: string;
};

function ProposalMeta({
    icon: Icon,
    label,
    value,
}: ProposalMetaProps) {
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

function ProposalStatusBadge({
    status,
}: {
    status: ProposalStatus;
}) {
    return (
        <span
            className={cn(
                "rounded-full border px-2.5 py-1 text-xs font-medium",
                proposalStatusStyles[
                status
                ],
            )}
        >
            {status}
        </span>
    );
}

const proposalStatusStyles: Record<
    ProposalStatus,
    string
> = {
    Pending:
        "border-amber-500/30 bg-amber-500/10 text-amber-400",

    Accepted:
        "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",

    Rejected:
        "border-red-500/30 bg-red-500/10 text-red-400",

    Withdrawn:
        "border-slate-500/30 bg-slate-500/10 text-slate-300",
};

function ProposalPagination({
    page,
    totalPages,
    hasPreviousPage,
    hasNextPage,
    isFetching,
    onPrevious,
    onNext,
}: {
    page: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
    isFetching: boolean;
    onPrevious: () => void;
    onNext: () => void;
}) {
    return (
        <div className="mt-6 flex flex-col gap-3 border-t pt-5 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-muted-foreground">
                Page {page} of{" "}
                {totalPages}
            </p>

            <div className="flex items-center gap-2">
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={
                        !hasPreviousPage ||
                        isFetching
                    }
                    onClick={onPrevious}
                >
                    <ChevronLeft className="size-4" />
                    Previous
                </Button>

                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={
                        !hasNextPage ||
                        isFetching
                    }
                    onClick={onNext}
                >
                    Next
                    <ChevronRight className="size-4" />
                </Button>
            </div>
        </div>
    );
}

function ProjectProposalsEmpty() {
    return (
        <div className="mt-6 rounded-xl border border-dashed p-8 text-center">
            <BriefcaseBusiness className="mx-auto size-8 text-muted-foreground" />

            <p className="mt-3 font-medium">
                No proposals yet
            </p>

            <p className="mx-auto mt-1 max-w-md text-sm leading-6 text-muted-foreground">
                Freelancer proposals will
                appear here after they are
                submitted.
            </p>
        </div>
    );
}

function ProjectProposalsError({
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
                            Could not load proposals
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

function ProjectProposalsSkeleton() {
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
                        className="h-44 rounded-xl border bg-muted/40"
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

function formatDateTime(
    value: string,
) {
    const date = new Date(value);

    if (
        Number.isNaN(date.getTime())
    ) {
        return "at an unknown time";
    }

    return new Intl.DateTimeFormat(
        "en-US",
        {
            dateStyle: "medium",
            timeStyle: "short",
        },
    ).format(date);
}