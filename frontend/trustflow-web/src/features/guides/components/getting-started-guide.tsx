import type { LucideIcon } from "lucide-react";
import {
    ArrowRight,
    BookOpenCheck,
    CheckCircle2,
    Clock3,
    ShieldCheck,
} from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";

export type GuideStep = {
    title: string;
    description: string;
    icon: LucideIcon;
    action?: {
        label: string;
        href: string;
    };
};

type GettingStartedGuideProps = {
    role: "Client" | "Freelancer";
    title: string;
    description: string;
    startAction: {
        label: string;
        href: string;
    };
    steps: GuideStep[];
    reminders: string[];
};

export function GettingStartedGuide({
    role,
    title,
    description,
    startAction,
    steps,
    reminders,
}: GettingStartedGuideProps) {
    return (
        <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
            <section className="relative overflow-hidden rounded-2xl border bg-card/80 p-6 shadow-blue-glow sm:p-8">
                <div className="pointer-events-none absolute -right-16 -top-20 size-64 rounded-full bg-primary/10 blur-3xl" />

                <div className="relative max-w-3xl">
                    <div className="flex size-12 items-center justify-center rounded-xl border border-primary/30 bg-primary/10">
                        <BookOpenCheck className="size-6 text-electric" />
                    </div>

                    <p className="mt-6 text-sm font-medium uppercase tracking-[0.22em] text-electric">
                        {role} guide
                    </p>

                    <h1 className="mt-3 text-3xl font-semibold sm:text-4xl">
                        {title}
                    </h1>

                    <p className="mt-4 max-w-2xl leading-7 text-muted-foreground">
                        {description}
                    </p>

                    <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:items-center">
                        <Button asChild size="lg" className="shadow-blue-glow">
                            <Link href={startAction.href}>
                                {startAction.label}
                                <ArrowRight className="size-4" />
                            </Link>
                        </Button>

                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            <Clock3 className="size-4 text-electric" />
                            About 3 minutes to read
                        </div>
                    </div>
                </div>
            </section>

            <section className="mt-8" aria-labelledby="guide-steps-title">
                <div>
                    <p className="text-sm font-medium uppercase tracking-[0.2em] text-electric">
                        Start to finish
                    </p>
                    <h2 id="guide-steps-title" className="mt-2 text-2xl font-semibold">
                        Follow these steps
                    </h2>
                    <p className="mt-2 text-sm text-muted-foreground">
                        Do them in order. You can come back to this page at any time.
                    </p>
                </div>

                <ol className="mt-6 grid gap-4 md:grid-cols-2">
                    {steps.map((step, index) => {
                        const Icon = step.icon;

                        return (
                            <li
                                key={step.title}
                                className="relative overflow-hidden rounded-2xl border bg-card/60 p-5 sm:p-6"
                            >
                                <div className="flex items-start gap-4">
                                    <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary/10">
                                        <Icon className="size-5 text-electric" />
                                    </div>

                                    <div className="min-w-0 flex-1">
                                        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-electric">
                                            Step {index + 1}
                                        </p>
                                        <h3 className="mt-2 text-lg font-semibold">
                                            {step.title}
                                        </h3>
                                        <p className="mt-2 text-sm leading-6 text-muted-foreground">
                                            {step.description}
                                        </p>

                                        {step.action && (
                                            <Button
                                                asChild
                                                variant="outline"
                                                size="sm"
                                                className="mt-4"
                                            >
                                                <Link href={step.action.href}>
                                                    {step.action.label}
                                                    <ArrowRight className="size-3.5" />
                                                </Link>
                                            </Button>
                                        )}
                                    </div>
                                </div>
                            </li>
                        );
                    })}
                </ol>
            </section>

            <section className="mt-8 rounded-2xl border border-primary/25 bg-primary/5 p-5 sm:p-6">
                <div className="flex items-center gap-3">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10">
                        <ShieldCheck className="size-5 text-electric" />
                    </div>
                    <div>
                        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-electric">
                            Stay safe
                        </p>
                        <h2 className="mt-1 text-lg font-semibold">
                            Remember these rules
                        </h2>
                    </div>
                </div>

                <ul className="mt-5 grid gap-3 md:grid-cols-2">
                    {reminders.map((reminder) => (
                        <li key={reminder} className="flex gap-3 text-sm leading-6 text-muted-foreground">
                            <CheckCircle2 className="mt-1 size-4 shrink-0 text-emerald-400" />
                            <span>{reminder}</span>
                        </li>
                    ))}
                </ul>
            </section>
        </main>
    );
}
