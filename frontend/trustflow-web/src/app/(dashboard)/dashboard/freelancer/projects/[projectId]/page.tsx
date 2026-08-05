import {
    FreelancerProjectWorkspaceScreen,
    FreelancerWorkspaceSkeleton,
} from "@/features/projects/components/freelancer-project-workspace-screen";
import { Suspense } from "react";

type FreelancerProjectWorkspacePageProps = {
    params: Promise<{
        projectId: string;
    }>;
};

export default async function FreelancerProjectWorkspacePage({
    params,
}: FreelancerProjectWorkspacePageProps) {
    const {
        projectId,
    } = await params;

    return (
        <Suspense
            fallback={
                <FreelancerWorkspaceSkeleton />
            }
        >
            <FreelancerProjectWorkspaceScreen
                projectId={projectId}
            />
        </Suspense>
    );
}