"use client";

import { ProjectForm } from
    "@/features/projects/components/project-form";
import { useCreateProject } from
    "@/features/projects/hooks/use-create-project";

export function CreateProjectForm() {
    const createProjectMutation =
        useCreateProject();

    return (
        <ProjectForm
            defaultValues={{
                title: "",
                description: "",
                budget: "",
                deadline: "",
            }}
            isPending={
                createProjectMutation.isPending
            }
            submitLabel="Create project"
            pendingLabel="Creating project..."
            cancelHref="/dashboard/client/projects"
            onSubmit={(values) => {
                createProjectMutation.mutate(
                    values,
                );
            }}
        />
    );
}