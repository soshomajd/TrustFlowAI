"use client";

import type { LucideIcon } from
    "lucide-react";
import {
    BriefcaseBusiness,
    Clock3,
    LoaderCircle,
    RefreshCw,
    Send,
    TriangleAlert,
} from "lucide-react";
import Link from "next/link";

import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from "@/components/motion/animation-primitives";
import { Button } from
    "@/components/ui/button";
import { useFreelancerDashboardSummary } from
    "@/features/projects/hooks/use-freelancer-dashboard-summary";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";
import { cn } from "@/lib/utils";

type SummaryItem = {
    label: string;
    description: string;
    value: number;
    href: string;
    icon: LucideIcon;
    requiresAttention?: boolean;
};

export function FreelancerDashboardSummarySection() {
    const summaryQuery =
        useFreelancerDashboardSummary();

    if (summaryQuery.isPending) {
        return (
            <FreelancerDashboardSummarySkeleton />
        );
    }

    if (summaryQuery.isError) {
        return (
            <AnimatedSection className="mt-6">
                <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-5">
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                        <div className="flex gap-3">
                            <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                            <div>
                                <h2 className="font-semibold">
                                    Could not load dashboard
                                    summary
                                </h2>

                                <p className="mt-1 text-sm text-muted-foreground">
                                    {getApiErrorMessage(
                                        summaryQuery.error,
                                        "Dashboard summary could not be loaded.",
                                    )}
                                </p>
                            </div>
                        </div>

                        <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            disabled={
                                summaryQuery.isFetching
                            }
                            onClick={() => {
                                void summaryQuery.refetch();
                            }}
                        >
                            {summaryQuery.isFetching ? (
                                <LoaderCircle className="size-4 animate-spin" />
                            ) : (
                                <RefreshCw className="size-4" />
                            )}

                            {summaryQuery.isFetching
                                ? "Trying again..."
                                : "Try again"}
                        </Button>
                    </div>
                </section>
            </AnimatedSection>
        );
    }

    const summary = summaryQuery.data;

    const items: SummaryItem[] = [
        {
            label: "Total proposals",
            description:
                "All proposals you have submitted.",
            value: summary.totalProposals,
            href: "/dashboard/freelancer/proposals",
            icon: Send,
        },
        {
            label: "Pending proposals",
            description:
                "Proposals waiting for a client decision.",
            value: summary.pendingProposals,
            href: "/dashboard/freelancer/proposals",
            icon: Clock3,
        },
        {
            label: "Assigned projects",
            description:
                "Projects currently assigned to you.",
            value: summary.assignedProjects,
            href: "/dashboard/freelancer/projects",
            icon: BriefcaseBusiness,
        },
        {
            label: "Needs attention",
            description:
                "Rejected milestones that require changes.",
            value: summary.rejectedMilestones,
            href: "/dashboard/freelancer/projects",
            icon: TriangleAlert,
            requiresAttention:
                summary.rejectedMilestones > 0,
        },
    ];

    return (
        <AnimatedSection className="mt-6">
            <section>
                <div className="mb-4 flex items-center justify-between gap-4">
                    <div>
                        <h2 className="text-xl font-semibold">
                            Workspace overview
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            A live summary of your
                            freelancer activity.
                        </p>
                    </div>

                    {summaryQuery.isFetching && (
                        <span className="flex items-center gap-2 text-xs text-muted-foreground">
                            <LoaderCircle className="size-3.5 animate-spin" />
                            Updating
                        </span>
                    )}
                </div>

                <StaggerContainer className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                    {items.map((item) => (
                        <StaggerItem key={item.label}>
                            <SummaryCard item={item} />
                        </StaggerItem>
                    ))}
                </StaggerContainer>
            </section>
        </AnimatedSection>
    );
}

function SummaryCard({
    item,
}: {
    item: SummaryItem;
}) {
    const Icon = item.icon;

    return (
        <Link
            href={item.href}
            className={cn(
                "group flex h-full flex-col rounded-2xl border bg-card/60 p-5 transition hover:-translate-y-0.5 hover:border-primary/40 hover:bg-card/80",

                item.requiresAttention &&
                "border-red-500/40 bg-red-500/[0.06] shadow-[inset_12px_0_30px_-24px_rgba(239,68,68,0.9)] hover:border-red-500/50",
            )}
        >
            <div className="flex items-start justify-between gap-4">
                <div
                    className={cn(
                        "flex size-11 items-center justify-center rounded-xl bg-primary/10",

                        item.requiresAttention &&
                        "bg-red-500/10",
                    )}
                >
                    <Icon
                        className={cn(
                            "size-5 text-electric",

                            item.requiresAttention &&
                            "text-red-400",
                        )}
                    />
                </div>

                {item.requiresAttention && (
                    <span className="rounded-full border border-red-500/30 bg-red-500/10 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wider text-red-300">
                        Action required
                    </span>
                )}
            </div>

            <p
                className={cn(
                    "mt-5 text-3xl font-semibold",

                    item.requiresAttention &&
                    "text-red-300",
                )}
            >
                {item.value.toLocaleString()}
            </p>

            <h3 className="mt-2 font-semibold transition group-hover:text-electric">
                {item.label}
            </h3>

            <p className="mt-2 flex-1 text-sm leading-6 text-muted-foreground">
                {item.description}
            </p>
        </Link>
    );
}

function FreelancerDashboardSummarySkeleton() {
    return (
        <section className="mt-6">
            <div className="mb-4 space-y-2">
                <div className="h-6 w-44 animate-pulse rounded bg-muted/50" />
                <div className="h-4 w-64 animate-pulse rounded bg-muted/30" />
            </div>

            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {Array.from({
                    length: 4,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="h-52 animate-pulse rounded-2xl border bg-muted/20"
                    />
                ))}
            </div>
        </section>
    );
}