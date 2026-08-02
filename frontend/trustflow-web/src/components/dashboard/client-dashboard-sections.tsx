import { ClientDashboardStats } from
    "@/features/projects/components/client-dashboard-stats";
import { ClientProjectsOverview } from
    "@/features/projects/components/client-projects-overview";

export function ClientDashboardSections() {
    return (
        <div className="mt-8 space-y-8">
            <ClientDashboardStats />

            <ClientProjectsOverview />
        </div>
    );
}