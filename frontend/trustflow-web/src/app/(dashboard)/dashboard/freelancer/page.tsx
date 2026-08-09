import type { LucideIcon } from "lucide-react";
import { FreelancerDashboardSummarySection, } from "@/features/projects/components/freelancer-dashboard-summary-section";
import {
    ArrowRight,
    BriefcaseBusiness,
    CodeXml,
    FolderKanban,
    Search,
    Send,
    Sparkles,
} from "lucide-react";
import Link from "next/link";

import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from "@/components/motion/animation-primitives";
import { Button } from "@/components/ui/button";

type FreelancerAction = {
    title: string;
    description: string;
    actionLabel: string;
    href: string;
    icon: LucideIcon;
};

const freelancerActions: FreelancerAction[] = [
    {
        title: "Browse marketplace",
        description:
            "Discover open projects, review their milestones and submit proposals to clients.",
        actionLabel: "Find projects",
        href: "/dashboard/freelancer/marketplace",
        icon: Search,
    },
    {
        title: "My proposals",
        description:
            "Track pending, accepted, rejected and withdrawn proposals from one place.",
        actionLabel: "View proposals",
        href: "/dashboard/freelancer/proposals",
        icon: Send,
    },
    {
        title: "Assigned projects",
        description:
            "Manage accepted projects, start milestones and submit completed work for review.",
        actionLabel: "Open projects",
        href: "/dashboard/freelancer/projects",
        icon: BriefcaseBusiness,
    },
];

const workflowSteps = [
    {
        number: "01",
        title: "Find a project",
        description:
            "Browse marketplace projects that match your skills.",
    },
    {
        number: "02",
        title: "Submit a proposal",
        description:
            "Send your bid, estimated delivery time and cover letter.",
    },
    {
        number: "03",
        title: "Get assigned",
        description:
            "After the client accepts your proposal, the project enters your workspace.",
    },
    {
        number: "04",
        title: "Complete milestones",
        description:
            "Start, complete and submit milestones in the correct sequence.",
    },
];

export default function FreelancerDashboardPage() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <AnimatedSection>
                <section className="relative overflow-hidden rounded-2xl border bg-card/80 p-6 shadow-blue-glow backdrop-blur sm:p-8">
                    <div className="pointer-events-none absolute -right-20 -top-20 size-64 rounded-full bg-primary/10 blur-3xl" />

                    <div className="relative">
                        <div className="flex size-12 items-center justify-center rounded-xl bg-primary/10">
                            <CodeXml className="size-6 text-electric" />
                        </div>

                        <p className="mt-6 text-sm font-medium uppercase tracking-[0.25em] text-electric">
                            Freelancer workspace
                        </p>

                        <h1 className="mt-3 text-3xl font-semibold sm:text-4xl">
                            Freelancer dashboard
                        </h1>

                        <p className="mt-4 max-w-2xl leading-7 text-muted-foreground">
                            Find new opportunities, manage your
                            proposals and complete assigned project
                            milestones from one workspace.
                        </p>

                        <div className="mt-7 flex flex-col gap-3 sm:flex-row">
                            <Button
                                asChild
                                className="shadow-blue-glow"
                            >
                                <Link href="/dashboard/freelancer/marketplace">
                                    Browse marketplace
                                    <ArrowRight className="size-4" />
                                </Link>
                            </Button>

                            <Button
                                asChild
                                variant="outline"
                            >
                                <Link href="/dashboard/freelancer/projects">
                                    <FolderKanban className="size-4" />
                                    Assigned projects
                                </Link>
                            </Button>
                        </div>
                    </div>
                </section>
            </AnimatedSection>
            <FreelancerDashboardSummarySection />
            <AnimatedSection className="mt-6">
                <section>
                    <header className="mb-4">
                        <div className="flex items-center gap-2">
                            <Sparkles className="size-5 text-electric" />

                            <h2 className="text-xl font-semibold">
                                Freelancer tools
                            </h2>
                        </div>

                        <p className="mt-2 text-sm text-muted-foreground">
                            Access every part of your freelancer
                            workflow.
                        </p>
                    </header>

                    <StaggerContainer className="grid gap-4 lg:grid-cols-3">
                        {freelancerActions.map((action) => (
                            <StaggerItem key={action.title}>
                                <FreelancerActionCard
                                    action={action}
                                />
                            </StaggerItem>
                        ))}
                    </StaggerContainer>
                </section>
            </AnimatedSection>

            <AnimatedSection className="mt-6">
                <section className="rounded-2xl border bg-card/60 p-5 backdrop-blur sm:p-6">
                    <header>
                        <h2 className="text-xl font-semibold">
                            How your workflow works
                        </h2>

                        <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
                            Follow these steps from finding a
                            project to completing its milestones.
                        </p>
                    </header>

                    <StaggerContainer className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                        {workflowSteps.map((step) => (
                            <StaggerItem key={step.number}>
                                <article className="h-full rounded-xl border bg-background/30 p-5">
                                    <span className="text-sm font-semibold text-electric">
                                        {step.number}
                                    </span>

                                    <h3 className="mt-4 font-semibold">
                                        {step.title}
                                    </h3>

                                    <p className="mt-2 text-sm leading-6 text-muted-foreground">
                                        {step.description}
                                    </p>
                                </article>
                            </StaggerItem>
                        ))}
                    </StaggerContainer>
                </section>
            </AnimatedSection>
        </main>
    );
}

function FreelancerActionCard({
    action,
}: {
    action: FreelancerAction;
}) {
    const Icon = action.icon;

    return (
        <Link
            href={action.href}
            className="group flex h-full flex-col rounded-2xl border bg-card/60 p-5 transition hover:-translate-y-0.5 hover:border-primary/50 hover:bg-card/80"
        >
            <div className="flex size-11 items-center justify-center rounded-xl bg-primary/10 transition group-hover:bg-primary/15">
                <Icon className="size-5 text-electric" />
            </div>

            <h3 className="mt-5 text-lg font-semibold transition group-hover:text-electric">
                {action.title}
            </h3>

            <p className="mt-2 flex-1 text-sm leading-6 text-muted-foreground">
                {action.description}
            </p>

            <div className="mt-6 flex items-center justify-between border-t pt-4 text-sm font-medium">
                <span>{action.actionLabel}</span>

                <ArrowRight className="size-4 transition-transform group-hover:translate-x-1" />
            </div>
        </Link>
    );
}