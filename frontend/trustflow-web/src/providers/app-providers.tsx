"use client";

import { QueryClient, QueryClientProvider, } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { MotionConfig } from "motion/react";
import { type ReactNode, useState, } from "react";
import { WagmiProvider } from "wagmi";
import { AuthBootstrap } from "@/features/auth/components/auth-bootstrap";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";


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
            <WagmiProvider config={wagmiConfig}>
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
            </WagmiProvider>
        </MotionConfig>
    );
}