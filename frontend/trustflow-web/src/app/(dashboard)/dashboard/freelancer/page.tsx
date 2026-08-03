import {
    ArrowRight,
    CodeXml,
} from "lucide-react";
import Link from "next/link";

import { Button } from
    "@/components/ui/button";

export default function FreelancerDashboardPage() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <div className="rounded-2xl border bg-card/80 p-8 shadow-blue-glow backdrop-blur">
                <div className="flex size-12 items-center justify-center rounded-xl bg-primary/10">
                    <CodeXml className="size-6 text-electric" />
                </div>

                <p className="mt-6 text-sm font-medium uppercase tracking-[0.25em] text-electric">
                    Freelancer workspace
                </p>

                <h1 className="mt-3 text-3xl font-semibold">
                    Freelancer dashboard
                </h1>

                <p className="mt-3 max-w-2xl text-muted-foreground">
                    Browse open projects,
                    submit proposals and manage
                    assigned milestones from your
                    freelancer workspace.
                </p>

                <div className="mt-6">
                    <Button asChild>
                        <Link href="/dashboard/freelancer/marketplace">
                            Browse marketplace
                            <ArrowRight className="size-4" />
                        </Link>
                    </Button>
                </div>
            </div>
        </main>
    );
}