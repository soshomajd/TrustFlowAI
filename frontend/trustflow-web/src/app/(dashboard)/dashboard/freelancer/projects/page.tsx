import { Suspense } from "react";

import {
    AssignedProjectsPageSkeleton,
    AssignedProjectsScreen,
} from "@/features/projects/components/assigned-projects-screen";

export default function FreelancerProjectsPage() {
    return (
        <Suspense
            fallback={
                <AssignedProjectsPageSkeleton />
            }
        >
            <AssignedProjectsScreen />
        </Suspense>
    );
}