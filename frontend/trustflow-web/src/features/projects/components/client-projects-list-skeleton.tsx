export function ClientProjectsListSkeleton() {
    return (
        <section className="mt-6 overflow-hidden rounded-2xl border bg-card/70">
            <header className="flex items-center justify-between border-b p-5">
                <div>
                    <div className="h-5 w-32 animate-pulse rounded bg-muted" />

                    <div className="mt-2 h-3 w-44 animate-pulse rounded bg-muted" />
                </div>

                <div className="h-4 w-20 animate-pulse rounded bg-muted" />
            </header>

            <div className="divide-y">
                {Array.from({
                    length: 5,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="p-5"
                    >
                        <div className="flex items-center justify-between gap-4">
                            <div className="h-5 w-1/3 animate-pulse rounded bg-muted" />

                            <div className="h-6 w-20 animate-pulse rounded-full bg-muted" />
                        </div>

                        <div className="mt-3 h-4 w-2/3 animate-pulse rounded bg-muted" />

                        <div className="mt-5 grid gap-4 sm:grid-cols-3">
                            <div className="h-10 animate-pulse rounded bg-muted" />
                            <div className="h-10 animate-pulse rounded bg-muted" />
                            <div className="h-10 animate-pulse rounded bg-muted" />
                        </div>
                    </div>
                ))}
            </div>
        </section>
    );
}