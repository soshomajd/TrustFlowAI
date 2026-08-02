import type { Metadata } from
    "next";

import { ProjectWorkspaceScreen } from
    "@/features/projects/components/project-workspace-screen";

export const metadata: Metadata = {
    title: "Project workspace",
};

type ProjectWorkspacePageProps = {
    params: Promise<{
        projectId: string;
    }>;
};

export default async function ProjectWorkspacePage({
    params,
}: ProjectWorkspacePageProps) {
    const { projectId } =
        await params;

    return (
        <ProjectWorkspaceScreen
            projectId={projectId}
        />
    );
}