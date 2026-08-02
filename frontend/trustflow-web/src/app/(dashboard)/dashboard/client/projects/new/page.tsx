import type { Metadata } from "next";
import { FilePlus2 } from
    "lucide-react";

import { CreateProjectForm } from
    "@/features/projects/components/create-project-form";

export const metadata: Metadata = {
    title: "Create project",
};

export default function CreateProjectPage() {
    return (
        <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6">
            <header>
                <div className="flex items-center gap-2 text-sm font-medium uppercase tracking-[0.22em] text-electric">
                    <FilePlus2 className="size-4" />
                    Client workspace
                </div>

                <h1 className="mt-3 text-3xl font-semibold">
                    Create a new project
                </h1>

                <p className="mt-2 max-w-2xl leading-7 text-muted-foreground">
                    Add the project details,
                    budget and final deadline.
                    Milestones can be managed
                    after the project is created.
                </p>
            </header>

            <div className="mt-8">
                <CreateProjectForm />
            </div>
        </main>
    );
}