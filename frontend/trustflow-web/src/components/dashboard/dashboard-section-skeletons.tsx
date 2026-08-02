export function ClientDashboardStatsSkeleton() {
    return (
        <section className="space-y-4">
            <div>
                <div className="h-5 w-40 animate-pulse rounded bg-muted" />
                <div className="mt-2 h-4 w-64 animate-pulse rounded bg-muted" />
            </div>

            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {Array.from({
                    length: 6,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="rounded-2xl border bg-card/70 p-5"
                    >
                        <div className="flex items-center justify-between">
                            <div className="size-10 animate-pulse rounded-xl bg-muted" />

                            <div className="h-3 w-16 animate-pulse rounded bg-muted" />
                        </div>

                        <div className="mt-5 h-8 w-24 animate-pulse rounded bg-muted" />

                        <div className="mt-3 h-3 w-32 animate-pulse rounded bg-muted" />
                    </div>
                ))}
            </div>
        </section>
    );
}

export function ClientProjectsModuleSkeleton() {
    return (
        <section className="rounded-2xl border bg-card/70 p-5">
            <div className="flex items-center justify-between">
                <div>
                    <div className="h-5 w-36 animate-pulse rounded bg-muted" />

                    <div className="mt-2 h-3 w-48 animate-pulse rounded bg-muted" />
                </div>

                <div className="h-9 w-28 animate-pulse rounded-lg bg-muted" />
            </div>

            <div className="mt-6 space-y-4">
                {Array.from({
                    length: 3,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="rounded-xl border p-4"
                    >
                        <div className="h-4 w-1/3 animate-pulse rounded bg-muted" />

                        <div className="mt-3 h-3 w-2/3 animate-pulse rounded bg-muted" />

                        <div className="mt-5 h-2 w-full animate-pulse rounded bg-muted" />
                    </div>
                ))}
            </div>
        </section>
    );
}