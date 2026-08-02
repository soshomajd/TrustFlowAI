import {
    FolderKanban,
    Plus,
} from "lucide-react";
import Link from "next/link";
import { Suspense } from "react";

import { Button } from
    "@/components/ui/button";
import { ClientProjectsFilters } from
    "@/features/projects/components/client-projects-filters";

export default function ClientProjectsPage() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <header className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <div className="flex items-center gap-2 text-sm font-medium uppercase tracking-[0.22em] text-electric">
                        <FolderKanban className="size-4" />
                        Client workspace
                    </div>

                    <h1 className="mt-3 text-3xl font-semibold">
                        My projects
                    </h1>

                    <p className="mt-2 max-w-2xl text-muted-foreground">
                        Manage your projects, review
                        their status and track current
                        progress.
                    </p>
                </div>

                <Button
                    asChild
                    className="shadow-blue-glow"
                >
                    <Link href="/dashboard/client/projects/new">
                        <Plus className="size-4" />
                        Create project
                    </Link>
                </Button>
            </header>

            <Suspense
                fallback={
                    <ProjectsFiltersSkeleton />
                }
            >
                <ClientProjectsFilters />
            </Suspense>

            <section className="mt-6 rounded-2xl border border-dashed bg-card/40 p-8 text-center">
                <p className="text-sm text-muted-foreground">
                    Project list will be connected
                    in the next step.
                </p>
            </section>
        </main>
    );
}

function ProjectsFiltersSkeleton() {
    return (
        <section className="mt-8 flex h-[74px] animate-pulse items-center rounded-2xl border bg-card/70 p-4">
            <div className="h-10 w-48 rounded-lg bg-muted" />
        </section>
    );
}