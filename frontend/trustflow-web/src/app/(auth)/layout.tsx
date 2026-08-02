import type { ReactNode } from "react";

type AuthLayoutProps = {
    children: ReactNode;
};

export default function AuthLayout({
    children,
}: AuthLayoutProps) {
    return (
        <main className="relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-12">
            <div className="pointer-events-none absolute left-1/2 -top-72 size-152 -translate-x-1/2 rounded-full bg-primary/15 blur-[140px]" />

            <div className="pointer-events-none absolute -bottom-64 -right-40 size-120 rounded-full bg-electric/10 blur-[130px]" />

            <div className="relative z-10 flex w-full justify-center">
                {children}
            </div>
        </main>
    );
}