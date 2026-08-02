"use client";

import { QueryClient, QueryClientProvider, } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { MotionConfig } from "motion/react";
import { type ReactNode, useState, } from "react";
import { AuthBootstrap } from "@/features/auth/components/auth-bootstrap";


type AppProvidersProps = {
    children: ReactNode;
};

export function AppProviders({
    children,
}: AppProvidersProps) {
    const [queryClient] = useState(
        () =>
            new QueryClient({
                defaultOptions: {
                    queries: {
                        staleTime: 30_000,
                        gcTime: 5 * 60_000,
                        retry: 1,
                        refetchOnWindowFocus: false,
                    },

                    mutations: {
                        retry: false,
                    },
                },
            }),
    );

    return (
        <MotionConfig reducedMotion="user">
            <QueryClientProvider client={queryClient}>
                <AuthBootstrap />

                {children}

                {process.env.NODE_ENV ===
                    "development" && (
                        <ReactQueryDevtools
                            initialIsOpen={false}
                        />
                    )}
            </QueryClientProvider>
        </MotionConfig>
    );
}