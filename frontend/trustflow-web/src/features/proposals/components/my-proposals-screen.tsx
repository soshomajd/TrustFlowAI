"use client";

import {
    BriefcaseBusiness,
    CalendarDays,
    ChevronLeft,
    ChevronRight,
    CircleDollarSign,
    Clock3,
    FileText,
    LoaderCircle,
    RefreshCw,
    TriangleAlert,
} from "lucide-react";
import Link from "next/link";
import {
    usePathname,
    useRouter,
    useSearchParams,
} from "next/navigation";

import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from "@/components/motion/animation-primitives";
import { Button } from
    "@/components/ui/button";
import { WithdrawProposalDialog } from
    "@/features/proposals/components/withdraw-proposal-dialog";
import { useMyProposals } from
    "@/features/proposals/hooks/use-my-proposals";
import type {
    MyProposal,
    ProposalStatus,
} from
    "@/features/proposals/types/proposal";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { cn } from
    "@/lib/utils";

const PAGE_SIZE = 10;

export function MyProposalsScreen() {
    const router = useRouter();
    const pathname = usePathname();

    const searchParams =
        useSearchParams();

    const page = parsePage(
        searchParams.get("page"),
    );

    const proposalsQuery =
        useMyProposals({
            page,
            pageSize: PAGE_SIZE,
        });

    function changePage(
        nextPage: number,
    ) {
        const nextParams =
            new URLSearchParams(
                searchParams.toString(),
            );

        if (nextPage <= 1) {
            nextParams.delete("page");
        } else {
            nextParams.set(
                "page",
                String(nextPage),
            );
        }

        const query =
            nextParams.toString();

        router.replace(
            query
                ? `${pathname}?${query}`
                : pathname,
            {
                scroll: false,
            },
        );
    }

    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <AnimatedSection>
                <section className="rounded-2xl border bg-card/80 p-6 shadow-blue-glow backdrop-blur sm:p-8">
                    <div className="flex size-12 items-center justify-center rounded-xl bg-primary/10">
                        <FileText className="size-6 text-electric" />
                    </div>

                    <p className="mt-6 text-sm font-medium uppercase tracking-[0.25em] text-electric">
                        Freelancer proposals
                    </p>

                    <h1 className="mt-3 text-3xl font-semibold">
                        My proposals
                    </h1>

                    <p className="mt-3 max-w-2xl text-muted-foreground">
                        Track submitted proposals,
                        review their current status
                        and withdraw proposals that
                        are still pending.
                    </p>
                </section>
            </AnimatedSection>

            <MyProposalsResults
                query={proposalsQuery}
                onPrevious={() => {
                    changePage(page - 1);
                }}
                onNext={() => {
                    changePage(page + 1);
                }}
            />
        </main>
    );
}

type MyProposalsResultsProps = {
    query:
    ReturnType<
        typeof useMyProposals
    >;

    onPrevious: () => void;
    onNext: () => void;
};

function MyProposalsResults({
    query,
    onPrevious,
    onNext,
}: MyProposalsResultsProps) {
    if (query.isLoading) {
        return <MyProposalsSkeleton />;
    }

    if (query.isError) {
        return (
            <MyProposalsError
                message={getApiErrorMessage(
                    query.error,
                    "Your proposals could not be loaded.",
                )}
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
        return <MyProposalsEmpty />;
    }

    return (
        <AnimatedSection className="mt-6">
            <section className="rounded-2xl border bg-card/50 p-5 sm:p-6">
                <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                        <h2 className="text-xl font-semibold">
                            Submitted proposals
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            {response.totalItems}{" "}
                            {response.totalItems === 1
                                ? "proposal"
                                : "proposals"}
                        </p>
                    </div>

                    {query.isFetching && (
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            <LoaderCircle className="size-4 animate-spin" />
                            Updating proposals
                        </div>
                    )}
                </header>

                <StaggerContainer className="mt-6 space-y-4">
                    {response.items.map(
                        (proposal) => (
                            <StaggerItem
                                key={proposal.id}
                            >
                                <MyProposalCard
                                    proposal={proposal}
                                />
                            </StaggerItem>
                        ),
                    )}
                </StaggerContainer>

                {response.totalPages > 1 && (
                    <div className="mt-6 flex flex-col gap-3 border-t pt-5 sm:flex-row sm:items-center sm:justify-between">
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
                    </div>
                )}
            </section>
        </AnimatedSection>
    );
}

function MyProposalCard({
    proposal,
}: {
    proposal: MyProposal;
}) {
    const canWithdraw =
        proposal.status === "Pending";

    return (
        <article className="rounded-xl border bg-background/40 p-5 transition hover:border-primary/40 hover:bg-background/70">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-3">
                        <div className="flex items-center gap-2">
                            <BriefcaseBusiness className="size-4 text-electric" />

                            <h3 className="text-lg font-semibold">
                                {proposal.projectTitle}
                            </h3>
                        </div>

                        <ProposalStatusBadge
                            status={proposal.status}
                        />
                    </div>

                    <p className="mt-4 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">
                        {truncateText(
                            proposal.coverLetter,
                            500,
                        )}
                    </p>

                    <div className="mt-4 flex items-center gap-2 text-xs text-muted-foreground">
                        <CalendarDays className="size-3.5" />

                        Submitted{" "}
                        {formatDateTime(
                            proposal.createdAt,
                        )}
                    </div>
                </div>

                <div className="grid shrink-0 gap-3 sm:grid-cols-2 lg:min-w-64 lg:grid-cols-1">
                    <ProposalMeta
                        icon={CircleDollarSign}
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

            <div className="mt-5 flex flex-col gap-3 border-t pt-5 sm:flex-row sm:items-center sm:justify-end">
                {proposal.status ===
                    "Pending" && (
                        <Button
                            asChild
                            variant="outline"
                            size="sm"
                        >
                            <Link
                                href={`/dashboard/freelancer/marketplace/${proposal.projectId}`}
                            >
                                View project
                            </Link>
                        </Button>
                    )}

                {canWithdraw && (
                    <WithdrawProposalDialog
                        proposalId={proposal.id}
                        projectTitle={
                            proposal.projectTitle
                        }
                    />
                )}
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

function MyProposalsEmpty() {
    return (
        <section className="mt-6 rounded-2xl border border-dashed bg-card/40 p-10 text-center">
            <FileText className="mx-auto size-10 text-muted-foreground" />

            <h2 className="mt-4 text-lg font-semibold">
                No proposals yet
            </h2>

            <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">
                Browse the marketplace and
                submit your first proposal.
            </p>

            <Button
                asChild
                className="mt-6"
            >
                <Link href="/dashboard/freelancer/marketplace">
                    Browse marketplace
                </Link>
            </Button>
        </section>
    );
}

function MyProposalsError({
    message,
    onRetry,
}: {
    message: string;
    onRetry: () => void;
}) {
    return (
        <section className="mt-6 rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
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

function MyProposalsSkeleton() {
    return (
        <section className="mt-6 animate-pulse rounded-2xl border bg-card/50 p-6">
            <div className="h-7 w-48 rounded bg-muted" />

            <div className="mt-6 space-y-4">
                {Array.from({
                    length: 4,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="h-64 rounded-xl border bg-muted/40"
                    />
                ))}
            </div>
        </section>
    );
}

export function MyProposalsPageSkeleton() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <div className="h-64 animate-pulse rounded-2xl border bg-muted/30" />

            <MyProposalsSkeleton />
        </main>
    );
}

function parsePage(
    value: string | null,
) {
    const parsed = Number(value);

    if (
        !Number.isInteger(parsed) ||
        parsed < 1
    ) {
        return 1;
    }

    return parsed;
}

function truncateText(
    value: string,
    maximumLength: number,
) {
    if (
        value.length <= maximumLength
    ) {
        return value;
    }

    return `${value.slice(
        0,
        maximumLength,
    )}...`;
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