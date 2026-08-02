import type { LucideIcon } from "lucide-react";

type SectionPlaceholderProps = {
    eyebrow: string;
    title: string;
    description: string;
    icon: LucideIcon;
};

export function SectionPlaceholder({
    eyebrow,
    title,
    description,
    icon: Icon,
}: SectionPlaceholderProps) {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <div className="rounded-2xl border bg-card/70 p-8 backdrop-blur">
                <div className="flex size-12 items-center justify-center rounded-xl border border-primary/30 bg-primary/10">
                    <Icon className="size-6 text-electric" />
                </div>

                <p className="mt-6 text-xs font-semibold uppercase tracking-[0.25em] text-electric">
                    {eyebrow}
                </p>

                <h1 className="mt-3 text-3xl font-semibold">
                    {title}
                </h1>

                <p className="mt-3 max-w-2xl text-muted-foreground">
                    {description}
                </p>
            </div>
        </main>
    );
}