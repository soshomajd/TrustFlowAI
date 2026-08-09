"use client";

import {
    AlertCircle,
    LoaderCircle,
    RefreshCw,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { ProjectForm } from
    "@/features/projects/components/project-form";
import { useProjectWorkspace } from
    "@/features/projects/hooks/use-project-workspace";
import { useUpdateProject } from
    "@/features/projects/hooks/use-update-project";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";

type EditProjectFormProps = {
    projectId: string;
};

export function EditProjectForm({
    projectId,
}: EditProjectFormProps) {
    const router = useRouter();

    const workspaceQuery =
        useProjectWorkspace(projectId);

    const updateProjectMutation =
        useUpdateProject();

    if (workspaceQuery.isPending) {
        return (
            <section className="flex min-h-72 items-center justify-center rounded-2xl border bg-card/60">
                <div className="text-center">
                    <LoaderCircle className="mx-auto size-6 animate-spin text-electric" />

                    <p className="mt-3 text-sm text-muted-foreground">
                        Loading project...
                    </p>
                </div>
            </section>
        );
    }

    if (workspaceQuery.isError) {
        return (
            <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
                <AlertCircle className="size-6 text-destructive" />

                <h2 className="mt-4 font-semibold">
                    Could not load project
                </h2>

                <p className="mt-2 text-sm text-muted-foreground">
                    {getApiErrorMessage(
                        workspaceQuery.error,
                        "Project could not be loaded.",
                    )}
                </p>

                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="mt-5"
                    disabled={
                        workspaceQuery.isFetching
                    }
                    onClick={() => {
                        void workspaceQuery.refetch();
                    }}
                >
                    {workspaceQuery.isFetching ? (
                        <LoaderCircle className="size-4 animate-spin" />
                    ) : (
                        <RefreshCw className="size-4" />
                    )}

                    {workspaceQuery.isFetching
                        ? "Trying again..."
                        : "Try again"}
                </Button>
            </section>
        );
    }

    const workspace =
        workspaceQuery.data;

    if (!workspace) {
        return null;
    }

    if (workspace.status !== "Open") {
        return (
            <section className="rounded-2xl border border-amber-500/30 bg-amber-500/5 p-6">
                <AlertCircle className="size-6 text-amber-400" />

                <h2 className="mt-4 font-semibold">
                    Project cannot be edited
                </h2>

                <p className="mt-2 text-sm text-muted-foreground">
                    Only open projects can be edited.
                    This project is currently{" "}
                    {workspace.status}.
                </p>

                <Button
                    asChild
                    variant="outline"
                    size="sm"
                    className="mt-5"
                >
                    <Link
                        href={`/dashboard/client/projects/${projectId}`}
                    >
                        Back to project
                    </Link>
                </Button>
            </section>
        );
    }

    return (
        <ProjectForm
            defaultValues={{
                title: workspace.title,
                description:
                    workspace.description,
                budget: String(
                    workspace.budget,
                ),
                deadline: toDateInputValue(
                    workspace.deadline,
                ),
            }}
            isPending={
                updateProjectMutation.isPending
            }
            submitLabel="Save changes"
            pendingLabel="Saving changes..."
            cancelHref={`/dashboard/client/projects/${projectId}`}
            onSubmit={async (values) => {
                try {
                    await updateProjectMutation.mutateAsync({
                        projectId,
                        request: values,
                    });

                    toast.success(
                        "Project updated successfully.",
                    );

                    router.push(
                        `/dashboard/client/projects/${projectId}`,
                    );

                    router.refresh();
                } catch (error) {
                    toast.error(
                        getApiErrorMessage(
                            error,
                            "Project could not be updated.",
                        ),
                    );
                }
            }}
        />
    );
}

function toDateInputValue(
    value: string,
) {
    const date = new Date(value);

    const timezoneOffset =
        date.getTimezoneOffset() *
        60_000;

    return new Date(
        date.getTime() -
        timezoneOffset,
    )
        .toISOString()
        .slice(0, 10);
}