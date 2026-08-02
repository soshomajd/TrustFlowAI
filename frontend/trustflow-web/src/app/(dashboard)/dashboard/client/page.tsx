import { ClientDashboardSections } from
    "@/components/dashboard/client-dashboard-sections";

export default function ClientDashboardPage() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <section>
                <p className="text-sm font-medium uppercase tracking-[0.25em] text-electric">
                    Client workspace
                </p>

                <h1 className="mt-3 text-3xl font-semibold">
                    Dashboard overview
                </h1>

                <p className="mt-2 text-muted-foreground">
                    Review your project activity,
                    proposals and recent progress.
                </p>
            </section>

            <ClientDashboardSections />
        </main>
    );
}