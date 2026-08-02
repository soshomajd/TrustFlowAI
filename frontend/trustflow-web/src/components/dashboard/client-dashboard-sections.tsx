"use client";

import dynamic from "next/dynamic";

import {
    ClientDashboardStatsSkeleton,
    ClientProjectsModuleSkeleton,
} from
    "@/components/dashboard/dashboard-section-skeletons";

const ClientDashboardStats = dynamic(
    () =>
        import(
            "@/features/projects/components/client-dashboard-stats"
        ).then(
            (module) =>
                module.ClientDashboardStats,
        ),
    {
        loading: () => (
            <ClientDashboardStatsSkeleton />
        ),
    },
);

const ClientProjectsOverview = dynamic(
    () =>
        import(
            "@/features/projects/components/client-projects-overview"
        ).then(
            (module) =>
                module.ClientProjectsOverview,
        ),
    {
        loading: () => (
            <ClientProjectsModuleSkeleton />
        ),
    },
);

export function ClientDashboardSections() {
    return (
        <div className="mt-8 space-y-8">
            <ClientDashboardStats />

            <ClientProjectsOverview />
        </div>
    );
}