"use client";

import { QueryClient, QueryClientProvider, } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { MotionConfig } from "motion/react";
import { type ReactNode, useState, } from "react";
import { WagmiProvider } from "wagmi";
import { Toaster } from "@/components/ui/sonner";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";


type ProvidersProps = {
    children: ReactNode;
};

// Data fetching, motion settings and toasts. The sign-in screens need only this.
export function QueryProviders({
    children,
}: ProvidersProps) {
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
                {children}

                <Toaster
                    position="top-right"
                    richColors
                    closeButton
                />

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

// Everything above plus the wallet, for the signed-in dashboard.
export function AppProviders({
    children,
}: ProvidersProps) {
    return (
        <WagmiProvider config={wagmiConfig}>
            <QueryProviders>
                {children}
            </QueryProviders>
        </WagmiProvider>
    );
}
