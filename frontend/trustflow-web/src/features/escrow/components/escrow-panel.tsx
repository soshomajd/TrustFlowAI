"use client";

import {
    CheckCircle2,
    CircleDollarSign,
    Coins,
    Fuel,
    LoaderCircle,
    Network,
    Play,
    RefreshCw,
    Rocket,
    ShieldCheck,
    TriangleAlert,
    Wallet,
} from "lucide-react";
import { useState } from "react";
import {
    formatEther,
    formatUnits,
    type Address,
    zeroAddress,
} from "viem";
import {
    useBalance,
    useConnection,
    useReadContract,
    useSwitchChain,
} from "wagmi";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useCreateEscrow } from "@/features/escrow/hooks/use-create-escrow";
import { useDeployEscrow } from "@/features/escrow/hooks/use-deploy-escrow";
import { useFundEscrow } from "@/features/escrow/hooks/use-fund-escrow";
import { useMintTestTokens } from "@/features/escrow/hooks/use-mint-test-tokens";
import { useProjectEscrow } from "@/features/escrow/hooks/use-project-escrow";
import { useStartWork } from "@/features/escrow/hooks/use-start-work";
import { useSyncEscrow } from "@/features/escrow/hooks/use-sync-escrow";
import { erc20Abi } from "@/features/escrow/lib/erc20-abi";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import {
    LOCAL_HARDHAT_CHAIN_ID,
    LOCAL_MOCK_USDC_ADDRESS,
    MIN_LOCAL_GAS_BALANCE,
} from "@/features/escrow/lib/local-test-chain";
import { toBaseUnits } from "@/features/escrow/lib/token-amount";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";
import type { ProjectStatus } from "@/features/projects/types/project";
import { WalletConnectButton } from "@/features/wallet/components/wallet-connect-button";
import { useMyWallet } from "@/features/wallet/hooks/use-my-wallet";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { cn } from "@/lib/utils";

type EscrowPanelProps = {
    projectId: string;
    projectStatus: ProjectStatus;
    viewerRole: "Client" | "Freelancer";
};

export function EscrowPanel({
    projectId,
    projectStatus,
    viewerRole,
}: EscrowPanelProps) {
    const escrowQuery = useProjectEscrow(projectId);

    if (projectStatus !== "InProgress") {
        return null;
    }

    if (escrowQuery.isLoading) {
        return <EscrowPanelSkeleton />;
    }

    if (escrowQuery.isError) {
        return (
            <EscrowPanelError
                message={getApiErrorMessage(
                    escrowQuery.error,
                    "Escrow could not be loaded.",
                )}
                onRetry={() => {
                    void escrowQuery.refetch();
                }}
            />
        );
    }

    const escrow = escrowQuery.data;

    if (!escrow) {
        return viewerRole === "Client" ? (
            <CreateEscrowCard projectId={projectId} />
        ) : (
            <EscrowPanelShell title="Escrow" statusLabel="Not created yet">
                <p className="text-sm text-muted-foreground">
                    The client has not created an escrow for this
                    project yet.
                </p>
            </EscrowPanelShell>
        );
    }

    return (
        <EscrowDetailsCard
            projectId={projectId}
            escrow={escrow}
            viewerRole={viewerRole}
        />
    );
}

function CreateEscrowCard({ projectId }: { projectId: string }) {
    const [tokenAddress, setTokenAddress] = useState(
        LOCAL_MOCK_USDC_ADDRESS,
    );

    const connection = useConnection();
    const walletQuery = useMyWallet();
    const createMutation = useCreateEscrow(projectId);

    const isWalletReady = Boolean(
        connection.address &&
            walletQuery.data?.isVerified &&
            walletQuery.data.walletAddress?.toLowerCase() ===
                connection.address.toLowerCase(),
    );

    return (
        <EscrowPanelShell
            title="Escrow setup"
            statusLabel={
                isWalletReady ? "Wallet verified" : "Wallet required"
            }
            statusTone={isWalletReady ? "positive" : "warning"}
        >
            <p className="text-sm text-muted-foreground">
                Before the escrow can be created, connect the client
                wallet that will fund this project and prove ownership
                with a signature. Signing the verification message does
                not cost gas.
            </p>

            <div className="mt-5 rounded-xl border bg-background/35 p-4 sm:p-5">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex min-w-0 gap-3">
                        <div
                            className={cn(
                                "flex size-10 shrink-0 items-center justify-center rounded-full border text-sm font-semibold",
                                isWalletReady
                                    ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-400"
                                    : "border-primary/30 bg-primary/10 text-electric",
                            )}
                        >
                            {isWalletReady ? (
                                <CheckCircle2 className="size-5" />
                            ) : (
                                "1"
                            )}
                        </div>

                        <div className="min-w-0">
                            <p className="font-medium">
                                Connect and verify client wallet
                            </p>

                            <p className="mt-1 text-sm leading-6 text-muted-foreground">
                                {isWalletReady
                                    ? `Ready: ${truncateAddress(walletQuery.data?.walletAddress ?? undefined)}`
                                    : "Connect the wallet, then approve the ownership signature in your wallet."}
                            </p>
                        </div>
                    </div>

                    <div className="shrink-0">
                        {walletQuery.isLoading ? (
                            <div className="flex items-center gap-2 text-sm text-muted-foreground">
                                <LoaderCircle className="size-4 animate-spin" />
                                Checking wallet
                            </div>
                        ) : (
                            <WalletConnectButton />
                        )}
                    </div>
                </div>
            </div>

            {walletQuery.isError && (
                <ErrorNotice
                    message={getApiErrorMessage(
                        walletQuery.error,
                        "Wallet status could not be loaded.",
                    )}
                />
            )}

            <div
                className={cn(
                    "mt-5 border-t pt-5 transition-opacity",
                    !isWalletReady && "pointer-events-none opacity-45",
                )}
                aria-disabled={!isWalletReady}
            >
                <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
                    <div>
                        <Label htmlFor="escrow-token-address">
                            Token address
                        </Label>

                        <Input
                            id="escrow-token-address"
                            value={tokenAddress}
                            disabled={!isWalletReady}
                            onChange={(event) => {
                                setTokenAddress(event.target.value);
                            }}
                            className="mt-1.5 font-mono text-xs"
                        />
                    </div>

                    <Button
                        type="button"
                        disabled={
                            !isWalletReady ||
                            createMutation.isPending ||
                            tokenAddress.trim().length === 0
                        }
                        onClick={() => {
                            createMutation.mutate({
                                chainId: LOCAL_HARDHAT_CHAIN_ID,
                                tokenAddress: tokenAddress.trim(),
                            });
                        }}
                    >
                        {createMutation.isPending ? (
                            <LoaderCircle className="size-4 animate-spin" />
                        ) : (
                            <ShieldCheck className="size-4" />
                        )}
                        Create escrow
                    </Button>
                </div>
            </div>

            {createMutation.isError && (
                <ErrorNotice
                    message={getApiErrorMessage(
                        createMutation.error,
                        "Escrow could not be created.",
                    )}
                />
            )}
        </EscrowPanelShell>
    );
}

function EscrowDetailsCard({
    projectId,
    escrow,
    viewerRole,
}: {
    projectId: string;
    escrow: ProjectEscrow;
    viewerRole: "Client" | "Freelancer";
}) {
    const connection = useConnection();
    const walletQuery = useMyWallet();

    const isWalletVerified = Boolean(
        connection.address &&
            walletQuery.data?.isVerified &&
            walletQuery.data.walletAddress?.toLowerCase() ===
                connection.address.toLowerCase(),
    );

    const deployMutation = useDeployEscrow(projectId);
    const syncMutation = useSyncEscrow(projectId);
    const startWorkMutation = useStartWork(projectId);

    const workStartedQuery = useReadContract({
        address: escrow.contractAddress as Address | undefined,
        abi: escrowAbi,
        functionName: "workStarted",
        query: {
            enabled: Boolean(escrow.contractAddress),
            refetchInterval: 5_000,
        },
    });

    return (
        <EscrowPanelShell
            title="Escrow"
            statusLabel={formatStatusLabel(escrow.status)}
            statusTone={statusTone(escrow.status)}
        >
            <div className="grid gap-4 sm:grid-cols-3">
                <EscrowMeta
                    icon={CircleDollarSign}
                    label="Total"
                    value={formatAmount(escrow.totalAmount)}
                />

                <EscrowMeta
                    icon={CheckCircle2}
                    label="Released"
                    value={formatAmount(escrow.releasedAmount)}
                />

                <EscrowMeta
                    icon={ShieldCheck}
                    label="Contract"
                    value={
                        escrow.contractAddress
                            ? truncateAddress(escrow.contractAddress)
                            : "Not deployed"
                    }
                />
            </div>

            {escrow.status === "PendingDeployment" &&
                viewerRole === "Client" && (
                    <div className="mt-5 border-t pt-5">
                        <p className="mb-3 text-sm text-muted-foreground">
                            Deploy the escrow contract on-chain
                            before it can be funded.
                        </p>

                        <Button
                            type="button"
                            disabled={deployMutation.isPending}
                            onClick={() => {
                                deployMutation.mutate();
                            }}
                        >
                            {deployMutation.isPending ? (
                                <LoaderCircle className="size-4 animate-spin" />
                            ) : (
                                <Rocket className="size-4" />
                            )}
                            Deploy escrow
                        </Button>

                        {deployMutation.isError && (
                            <ErrorNotice
                                message={getApiErrorMessage(
                                    deployMutation.error,
                                    "Escrow could not be deployed.",
                                )}
                            />
                        )}
                    </div>
                )}

            {escrow.status === "Deploying" && (
                <div className="mt-5 flex items-center gap-2 border-t pt-5 text-sm text-muted-foreground">
                    <LoaderCircle className="size-4 animate-spin" />
                    Deploying the escrow contract on-chain...
                </div>
            )}

            {escrow.status === "AwaitingFunding" && (
                <div className="mt-5 border-t pt-5">
                    {viewerRole === "Client" ? (
                        <>
                            <p className="mb-3 text-sm text-muted-foreground">
                                Fund the escrow with the full
                                project budget. Your wallet will
                                be asked to approve the token
                                transfer and then fund the
                                contract.
                            </p>

                            {!isWalletVerified ? (
                                <WalletNotVerifiedNotice />
                            ) : (
                                <ClientFundingControls
                                    projectId={projectId}
                                    escrow={escrow}
                                />
                            )}
                        </>
                    ) : (
                        <p className="text-sm text-muted-foreground">
                            Waiting for the client to fund the
                            escrow before work can start.
                        </p>
                    )}

                    <SyncButton
                        isPending={syncMutation.isPending}
                        onClick={() => {
                            syncMutation.mutate();
                        }}
                    />
                </div>
            )}

            {escrow.status === "Funded" && (
                <div className="mt-5 border-t pt-5">
                    {viewerRole === "Freelancer" &&
                        workStartedQuery.data === false && (
                            <>
                                <p className="mb-3 text-sm text-muted-foreground">
                                    The escrow is funded. Start
                                    work on-chain before your
                                    first milestone can be
                                    released.
                                </p>

                                {!isWalletVerified ? (
                                    <WalletNotVerifiedNotice />
                                ) : (
                                    <Button
                                        type="button"
                                        disabled={
                                            startWorkMutation.isPending
                                        }
                                        onClick={() => {
                                            if (
                                                escrow.contractAddress
                                            ) {
                                                startWorkMutation.mutate(
                                                    escrow.contractAddress,
                                                );
                                            }
                                        }}
                                    >
                                        {startWorkMutation.isPending ? (
                                            <LoaderCircle className="size-4 animate-spin" />
                                        ) : (
                                            <Play className="size-4" />
                                        )}
                                        Start work
                                    </Button>
                                )}

                                {startWorkMutation.isError && (
                                    <ErrorNotice
                                        message={getWalletErrorMessage(
                                            startWorkMutation.error,
                                        )}
                                    />
                                )}
                            </>
                        )}

                    {(viewerRole === "Client" ||
                        workStartedQuery.data === true) && (
                        <p className="text-sm text-muted-foreground">
                            Escrow is funded and work has
                            started. Approve submitted milestones
                            to release payment.
                        </p>
                    )}
                </div>
            )}

            {escrow.status === "Completed" && (
                <div className="mt-5 border-t pt-5 text-sm text-muted-foreground">
                    All milestones have been released. The escrow
                    is complete.
                </div>
            )}
        </EscrowPanelShell>
    );
}

function ClientFundingControls({
    projectId,
    escrow,
}: {
    projectId: string;
    escrow: ProjectEscrow;
}) {
    const connection = useConnection();
    const walletQuery = useMyWallet();
    const fundMutation = useFundEscrow(projectId);
    const testFundsMutation = useMintTestTokens();
    const switchChainMutation = useSwitchChain();

    const expectedWalletAddress =
        walletQuery.data?.walletAddress ?? "";

    const checksEnabled = Boolean(
        connection.address && expectedWalletAddress,
    );

    const tokenDecimalsQuery = useReadContract({
        address: escrow.tokenAddress as Address,
        abi: erc20Abi,
        functionName: "decimals",
        chainId: LOCAL_HARDHAT_CHAIN_ID,
        query: {
            enabled: checksEnabled,
            staleTime: Number.POSITIVE_INFINITY,
        },
    });

    const tokenBalanceQuery = useReadContract({
        address: escrow.tokenAddress as Address,
        abi: erc20Abi,
        functionName: "balanceOf",
        args: [connection.address ?? zeroAddress],
        chainId: LOCAL_HARDHAT_CHAIN_ID,
        query: {
            enabled: checksEnabled,
            refetchInterval: 5_000,
        },
    });

    const gasBalanceQuery = useBalance({
        address: connection.address,
        chainId: LOCAL_HARDHAT_CHAIN_ID,
        query: {
            enabled: checksEnabled,
            refetchInterval: 5_000,
        },
    });

    const tokenDecimals = tokenDecimalsQuery.data;
    const requiredTokenAmount =
        tokenDecimals === undefined
            ? null
            : toBaseUnits(escrow.totalAmount, tokenDecimals);

    const tokenBalance = tokenBalanceQuery.data ?? 0n;
    const gasBalance = gasBalanceQuery.data?.value ?? 0n;

    const tokenShortfall =
        requiredTokenAmount === null ||
        tokenBalance >= requiredTokenAmount
            ? 0n
            : requiredTokenAmount - tokenBalance;

    const networkReady =
        escrow.chainId === LOCAL_HARDHAT_CHAIN_ID &&
        connection.chainId === LOCAL_HARDHAT_CHAIN_ID;

    const tokenReady =
        requiredTokenAmount !== null &&
        tokenBalanceQuery.isSuccess &&
        tokenBalance >= requiredTokenAmount;

    const gasReady =
        gasBalanceQuery.isSuccess &&
        gasBalance >= MIN_LOCAL_GAS_BALANCE;

    const checksLoading =
        tokenDecimalsQuery.isLoading ||
        tokenBalanceQuery.isLoading ||
        gasBalanceQuery.isLoading;

    const canFund =
        networkReady &&
        tokenReady &&
        gasReady &&
        !checksLoading;

    const localFaucetAvailable =
        process.env.NODE_ENV === "development" &&
        escrow.chainId === LOCAL_HARDHAT_CHAIN_ID &&
        escrow.tokenAddress.toLowerCase() ===
            LOCAL_MOCK_USDC_ADDRESS.toLowerCase();

    const balanceReadFailed =
        tokenDecimalsQuery.isError ||
        tokenBalanceQuery.isError ||
        gasBalanceQuery.isError;

    return (
        <div className="space-y-4">
            <div className="grid gap-3 md:grid-cols-3">
                <FundingReadinessItem
                    icon={Network}
                    label="Network"
                    value={
                        networkReady
                            ? "Localhost 31337"
                            : escrow.chainId === LOCAL_HARDHAT_CHAIN_ID
                              ? "Switch to Localhost 31337"
                              : `Unsupported chain ${escrow.chainId}`
                    }
                    ready={networkReady}
                />

                <FundingReadinessItem
                    icon={Coins}
                    label="Mock USDC"
                    value={formatTokenFundingBalance(
                        tokenBalance,
                        tokenDecimals,
                        escrow.totalAmount,
                        tokenBalanceQuery.isLoading ||
                            tokenDecimalsQuery.isLoading,
                    )}
                    ready={tokenReady}
                    loading={
                        tokenBalanceQuery.isLoading ||
                        tokenDecimalsQuery.isLoading
                    }
                />

                <FundingReadinessItem
                    icon={Fuel}
                    label="Gas"
                    value={
                        gasBalanceQuery.isLoading
                            ? "Checking balance"
                            : `${formatGasBalance(gasBalance)} ETH`
                    }
                    ready={gasReady}
                    loading={gasBalanceQuery.isLoading}
                />
            </div>

            {balanceReadFailed && (
                <ErrorNotice message="Local wallet balances could not be read. Make sure Hardhat and Mock USDC are running." />
            )}

            <div className="flex flex-wrap gap-3">
                {!networkReady &&
                    escrow.chainId === LOCAL_HARDHAT_CHAIN_ID && (
                        <Button
                            type="button"
                            variant="outline"
                            disabled={switchChainMutation.isPending}
                            onClick={() => {
                                switchChainMutation.mutate({
                                    chainId: LOCAL_HARDHAT_CHAIN_ID,
                                });
                            }}
                        >
                            {switchChainMutation.isPending ? (
                                <LoaderCircle className="size-4 animate-spin" />
                            ) : (
                                <Network className="size-4" />
                            )}
                            Switch to Localhost
                        </Button>
                    )}

                {localFaucetAvailable &&
                    networkReady &&
                    (!tokenReady || !gasReady) && (
                        <Button
                            type="button"
                            variant="outline"
                            disabled={
                                checksLoading ||
                                balanceReadFailed ||
                                testFundsMutation.isPending
                            }
                            onClick={() => {
                                testFundsMutation.mutate(
                                    {
                                        tokenAddress: escrow.tokenAddress,
                                        tokenShortfall,
                                        expectedWalletAddress,
                                    },
                                    {
                                        onSuccess: async () => {
                                            await Promise.all([
                                                tokenBalanceQuery.refetch(),
                                                gasBalanceQuery.refetch(),
                                            ]);
                                        },
                                    },
                                );
                            }}
                        >
                            {testFundsMutation.isPending ? (
                                <LoaderCircle className="size-4 animate-spin" />
                            ) : (
                                <Coins className="size-4" />
                            )}
                            Get test funds
                        </Button>
                    )}

                <Button
                    type="button"
                    disabled={
                        !canFund ||
                        fundMutation.isPending ||
                        testFundsMutation.isPending
                    }
                    onClick={() => {
                        fundMutation.mutate(escrow);
                    }}
                >
                    {fundMutation.isPending ? (
                        <LoaderCircle className="size-4 animate-spin" />
                    ) : (
                        <Wallet className="size-4" />
                    )}
                    Approve and fund escrow
                </Button>
            </div>

            {!canFund && !checksLoading && !balanceReadFailed && (
                <p className="text-xs leading-5 text-muted-foreground">
                    Approve and fund unlocks after the wallet is on
                    Localhost and has the full project budget plus gas.
                </p>
            )}

            {switchChainMutation.isError && (
                <ErrorNotice
                    message={getWalletErrorMessage(
                        switchChainMutation.error,
                        "The wallet could not switch networks.",
                    )}
                />
            )}

            {testFundsMutation.isError && (
                <ErrorNotice
                    message={getWalletErrorMessage(
                        testFundsMutation.error,
                        "Local test funds could not be prepared.",
                    )}
                />
            )}

            {fundMutation.isError && (
                <ErrorNotice
                    message={getWalletErrorMessage(fundMutation.error)}
                />
            )}
        </div>
    );
}

function FundingReadinessItem({
    icon: Icon,
    label,
    value,
    ready,
    loading = false,
}: {
    icon: typeof CircleDollarSign;
    label: string;
    value: string;
    ready: boolean;
    loading?: boolean;
}) {
    return (
        <div className="rounded-xl border bg-background/35 p-4">
            <div className="flex items-center justify-between gap-3">
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                    <Icon className="size-3.5" />
                    {label}
                </div>

                {loading ? (
                    <LoaderCircle className="size-4 animate-spin text-muted-foreground" />
                ) : ready ? (
                    <CheckCircle2 className="size-4 text-emerald-400" />
                ) : (
                    <TriangleAlert className="size-4 text-amber-400" />
                )}
            </div>

            <p className="mt-2 truncate text-sm font-medium">{value}</p>
        </div>
    );
}

function SyncButton({
    isPending,
    onClick,
}: {
    isPending: boolean;
    onClick: () => void;
}) {
    return (
        <Button
            type="button"
            variant="outline"
            size="sm"
            className="mt-3"
            disabled={isPending}
            onClick={onClick}
        >
            {isPending ? (
                <LoaderCircle className="size-3.5 animate-spin" />
            ) : (
                <RefreshCw className="size-3.5" />
            )}
            Refresh on-chain status
        </Button>
    );
}

function WalletNotVerifiedNotice() {
    return (
        <div className="flex flex-col gap-4 rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex gap-3 text-sm text-muted-foreground">
                <TriangleAlert className="mt-0.5 size-4 shrink-0 text-amber-400" />
                <p>
                    Connect the verified wallet for this account before
                    sending the transaction.
                </p>
            </div>

            <div className="shrink-0">
                <WalletConnectButton />
            </div>
        </div>
    );
}

function ErrorNotice({ message }: { message: string }) {
    return (
        <div
            role="alert"
            className="mt-3 flex gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4"
        >
            <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />
            <p className="text-sm text-destructive">{message}</p>
        </div>
    );
}

function EscrowMeta({
    icon: Icon,
    label,
    value,
}: {
    icon: typeof CircleDollarSign;
    label: string;
    value: string;
}) {
    return (
        <div className="rounded-xl border bg-card/40 px-4 py-3">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <Icon className="size-3.5" />
                {label}
            </div>

            <p className="mt-1 truncate text-sm font-medium">
                {value}
            </p>
        </div>
    );
}

function EscrowPanelShell({
    title,
    statusLabel,
    statusTone = "neutral",
    children,
}: {
    title: string;
    statusLabel: string;
    statusTone?: "neutral" | "positive" | "warning";
    children: React.ReactNode;
}) {
    return (
        <section className="rounded-2xl border bg-card/70 p-5 backdrop-blur sm:p-6">
            <div className="flex items-center justify-between gap-3">
                <h2 className="text-lg font-semibold">{title}</h2>

                <span
                    className={cn(
                        "rounded-full border px-2.5 py-1 text-xs font-medium",
                        statusTone === "positive" &&
                            "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",
                        statusTone === "warning" &&
                            "border-amber-500/30 bg-amber-500/10 text-amber-400",
                        statusTone === "neutral" &&
                            "border-slate-500/30 bg-slate-500/10 text-slate-300",
                    )}
                >
                    {statusLabel}
                </span>
            </div>

            <div className="mt-4">{children}</div>
        </section>
    );
}

function EscrowPanelSkeleton() {
    return (
        <section className="h-40 animate-pulse rounded-2xl border bg-muted/30" />
    );
}

function EscrowPanelError({
    message,
    onRetry,
}: {
    message: string;
    onRetry: () => void;
}) {
    return (
        <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex gap-3">
                    <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />
                    <div>
                        <h2 className="font-semibold">
                            Could not load escrow
                        </h2>
                        <p className="mt-1 text-sm text-muted-foreground">
                            {message}
                        </p>
                    </div>
                </div>

                <Button type="button" variant="outline" onClick={onRetry}>
                    <RefreshCw className="size-4" />
                    Try again
                </Button>
            </div>
        </section>
    );
}

function formatStatusLabel(status: ProjectEscrow["status"]) {
    return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function statusTone(
    status: ProjectEscrow["status"],
): "neutral" | "positive" | "warning" {
    if (status === "Funded" || status === "Completed") {
        return "positive";
    }

    if (status === "AwaitingFunding" || status === "Deploying") {
        return "warning";
    }

    return "neutral";
}

function formatAmount(value: number) {
    return new Intl.NumberFormat("en-US", {
        maximumFractionDigits: 2,
    }).format(value);
}

function formatTokenFundingBalance(
    balance: bigint,
    decimals: number | undefined,
    requiredAmount: number,
    loading: boolean,
) {
    if (loading || decimals === undefined) {
        return "Checking balance";
    }

    const displayBalance = Number(formatUnits(balance, decimals));

    return `${formatAmount(displayBalance)} / ${formatAmount(requiredAmount)} mUSDC`;
}

function formatGasBalance(balance: bigint) {
    return new Intl.NumberFormat("en-US", {
        maximumFractionDigits: 4,
    }).format(Number(formatEther(balance)));
}

function truncateAddress(address: string | null | undefined) {
    if (!address) {
        return "Unknown wallet";
    }

    return `${address.slice(0, 6)}...${address.slice(-4)}`;
}
