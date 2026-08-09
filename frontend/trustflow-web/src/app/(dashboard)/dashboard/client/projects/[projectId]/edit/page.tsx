import {
    ArrowLeft,
    Pencil,
} from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { EditProjectForm } from
    "@/features/projects/components/edit-project-form";

type EditProjectPageProps = {
    params: Promise<{
        projectId: string;
    }>;
};

export default async function EditProjectPage({
    params,
}: EditProjectPageProps) {
    const { projectId } =
        await params;

    return (
        <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6">
            <Button
                asChild
                variant="ghost"
                size="sm"
            >
                <Link
                    href={`/dashboard/client/projects/${projectId}`}
                >
                    <ArrowLeft className="size-4" />
                    Back to project
                </Link>
            </Button>

            <section className="mt-5 rounded-2xl border bg-card/70 p-6 backdrop-blur sm:p-8">
                <div className="flex size-12 items-center justify-center rounded-xl bg-primary/10">
                    <Pencil className="size-6 text-electric" />
                </div>

                <p className="mt-6 text-sm font-medium uppercase tracking-[0.25em] text-electric">
                    Project settings
                </p>

                <h1 className="mt-3 text-3xl font-semibold">
                    Edit project
                </h1>

                <p className="mt-3 max-w-2xl text-muted-foreground">
                    Update the project title,
                    description, budget and
                    deadline.
                </p>
            </section>

            <div className="mt-6">
                <EditProjectForm
                    projectId={projectId}
                />
            </div>
        </main>
    );
}