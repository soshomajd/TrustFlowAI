"use client";

import type { LucideIcon } from "lucide-react";
import {
    AlertCircle,
    CheckCircle2,
    CircleDot,
    FolderKanban,
    LoaderCircle,
    RefreshCw,
    Send,
    WalletCards,
    Workflow,
} from "lucide-react";
import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from
    "@/components/motion/animation-primitives";

import { Button } from
    "@/components/ui/button";
import { ClientDashboardStatsSkeleton } from
    "@/components/dashboard/dashboard-section-skeletons";
import { useClientDashboardSummary } from
    "@/features/projects/hooks/use-client-dashboard-summary";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";

type DashboardStat = {
    label: string;
    value: string;
    description: string;
    icon: LucideIcon;
};

export function ClientDashboardStats() {
    const summaryQuery =
        useClientDashboardSummary();

    if (summaryQuery.isPending) {
        return (
            <ClientDashboardStatsSkeleton />
        );
    }

    if (summaryQuery.isError) {
        return (
            <ClientDashboardStatsError
                error={summaryQuery.error}
                isRetrying={
                    summaryQuery.isFetching
                }
                onRetry={() => {
                    void summaryQuery.refetch();
                }}
            />
        );
    }

    const summary = summaryQuery.data;

    const stats: DashboardStat[] = [
        {
            label: "Total projects",
            value: formatNumber(
                summary.totalProjects,
            ),
            description:
                "All projects created by you",
            icon: FolderKanban,
        },
        {
            label: "Open projects",
            value: formatNumber(
                summary.openProjects,
            ),
            description:
                "Projects accepting proposals",
            icon: CircleDot,
        },
        {
            label: "In progress",
            value: formatNumber(
                summary.inProgressProjects,
            ),
            description:
                "Projects currently being worked on",
            icon: Workflow,
        },
        {
            label: "Completed",
            value: formatNumber(
                summary.completedProjects,
            ),
            description:
                "Successfully completed projects",
            icon: CheckCircle2,
        },
        {
            label: "Pending proposals",
            value: formatNumber(
                summary.pendingProposals,
            ),
            description:
                "Proposals waiting for review",
            icon: Send,
        },
        {
            label: "Total budget",
            value: formatAmount(
                summary.totalBudget,
            ),
            description:
                "Combined project budgets",
            icon: WalletCards,
        },
    ];

    return (
        <AnimatedSection>
            <section
                aria-labelledby="client-dashboard-stats-title"
                className="space-y-4"
            >
                <header className="flex items-end justify-between gap-4">
                    <div>
                        <h2
                            id="client-dashboard-stats-title"
                            className="font-semibold"
                        >
                            Project summary
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            A quick overview of your
                            project activity.
                        </p>
                    </div>

                    {summaryQuery.isFetching && (
                        <div className="flex items-center gap-2 text-xs text-muted-foreground">
                            <LoaderCircle className="size-3.5 animate-spin" />

                            Updating
                        </div>
                    )}
                </header>

                <StaggerContainer className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                    {stats.map((stat) => (
                        <StaggerItem
                            key={stat.label}
                            className="h-full"
                        >
                            <DashboardStatCard
                                stat={stat}
                            />
                        </StaggerItem>
                    ))}
                </StaggerContainer>
            </section>
        </AnimatedSection>
    );
}


type DashboardStatCardProps = {
    stat: DashboardStat;
};

function DashboardStatCard({
    stat,
}: DashboardStatCardProps) {
    const Icon = stat.icon;

    return (
        <article className="group h-full rounded-2xl border bg-card/70 p-5 backdrop-blur transition hover:border-primary/40 hover:bg-card">
            <div className="flex items-start justify-between">
                <div className="flex size-10 items-center justify-center rounded-xl border border-primary/20 bg-primary/10">
                    <Icon className="size-5 text-electric" />
                </div>

                <span className="text-xs font-medium text-muted-foreground">
                    {stat.label}
                </span>
            </div>

            <p className="mt-5 text-3xl font-semibold tracking-tight">
                {stat.value}
            </p>

            <p className="mt-2 text-sm text-muted-foreground">
                {stat.description}
            </p>
        </article>
    );
}

type ClientDashboardStatsErrorProps = {
    error: unknown;
    isRetrying: boolean;
    onRetry: () => void;
};

function ClientDashboardStatsError({
    error,
    isRetrying,
    onRetry,
}: ClientDashboardStatsErrorProps) {
    return (
        <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
            <div className="flex size-11 items-center justify-center rounded-xl bg-destructive/10">
                <AlertCircle className="size-5 text-destructive" />
            </div>

            <h2 className="mt-4 font-semibold">
                Could not load project summary
            </h2>

            <p className="mt-2 max-w-xl text-sm text-muted-foreground">
                {getApiErrorMessage(
                    error,
                    "Dashboard summary could not be loaded.",
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

function formatNumber(
    value: number,
) {
    return new Intl.NumberFormat(
        "en-US",
    ).format(value);
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