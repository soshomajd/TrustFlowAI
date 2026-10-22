"use client";
import { formatMoney as formatAmount, moneyText } from "@/lib/money/exact-money";

import { formatTokenPayment } from "@/features/escrow/lib/platform-fee";

import {
    Ban,
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
import {
    formatEther,
    type Address,
    zeroAddress,
} from "viem";
import {
    useBalance,
    useConnection,
    useReadContract,
    useSwitchChain,
} from "wagmi";

import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { useCancelEscrow } from "@/features/escrow/hooks/use-cancel-escrow";
import { useCaseAction } from "@/features/escrow/hooks/use-case-action";
import { useCreateEscrow } from "@/features/escrow/hooks/use-create-escrow";
import { useDeployEscrow } from "@/features/escrow/hooks/use-deploy-escrow";
import { useEscrowWorkStarted } from "@/features/escrow/hooks/use-escrow-work-started";
import { useFundEscrow } from "@/features/escrow/hooks/use-fund-escrow";
import { useMintTestTokens } from "@/features/escrow/hooks/use-mint-test-tokens";
import { useProjectEscrows } from "@/features/escrow/hooks/use-project-escrows";
import { useStartWork } from "@/features/escrow/hooks/use-start-work";
import { useSyncEscrow } from "@/features/escrow/hooks/use-sync-escrow";
import { erc20Abi } from "@/features/escrow/lib/erc20-abi";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import {
    ACTIVE_CHAIN_ID,
    ACTIVE_CHAIN_NAME,
    ACTIVE_TOKEN_ADDRESS,
    ACTIVE_TOKEN_SYMBOL,
    MIN_GAS_BALANCE,
    TEST_TOKEN_MINT_ENABLED,
} from "@/features/escrow/lib/network-config";
import { formatBaseUnits } from "@/lib/money/decimal-amount";
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

// A project can be split between several freelancers, and the contract pays
// one immutable address, so each freelancer gets their own escrow and their
// own card here. A freelancer only ever receives their own.
export function EscrowPanel({
    projectId,
    projectStatus,
    viewerRole,
}: EscrowPanelProps) {
    const escrowsQuery = useProjectEscrows(projectId);

    if (escrowsQuery.isLoading) {
        return <EscrowPanelSkeleton />;
    }

    if (escrowsQuery.isError) {
        return (
            <EscrowPanelError
                message={getApiErrorMessage(
                    escrowsQuery.error,
                    "Escrow could not be loaded.",
                )}
                onRetry={() => {
                    void escrowsQuery.refetch();
                }}
            />
        );
    }

    const participants = escrowsQuery.data ?? [];

    if (participants.length === 0) {
        if (projectStatus === "Open") return null;
        return (
            <EscrowPanelShell title="Escrow" statusLabel="No one assigned yet">
                <p className="text-sm text-muted-foreground">
                    No freelancer has been accepted for this project
                    yet, so there is nothing to pay into an escrow.
                </p>
            </EscrowPanelShell>
        );
    }

    // With a single freelancer the card reads as it always did; only a split
    // project needs each card labelled with its owner.
    const showOwner = participants.length > 1;

    return (
        <div className="space-y-5">
            {participants.map((participant) =>
                participant.escrow ? (
                    <EscrowDetailsCard
                        key={participant.freelancerId}
                        projectId={projectId}
                        escrow={participant.escrow}
                        ownerName={
                            showOwner
                                ? participant.freelancerFullName
                                : null
                        }
                        viewerRole={viewerRole}
                    />
                ) : viewerRole === "Client" ? (
                    <CreateEscrowCard
                        key={participant.freelancerId}
                        projectId={projectId}
                        freelancerId={participant.freelancerId}
                        freelancerFullName={
                            participant.freelancerFullName
                        }
                        assignedTotal={moneyText(participant, "assignedTotal")}
                        milestoneCount={participant.milestoneCount}
                    />
                ) : (
                    <EscrowPanelShell
                        key={participant.freelancerId}
                        title="Escrow"
                        statusLabel="Not created yet"
                    >
                        <p className="text-sm text-muted-foreground">
                            The client has not created an escrow for
                            your milestones on this project yet.
                        </p>
                    </EscrowPanelShell>
                ),
            )}
        </div>
    );
}

function CreateEscrowCard({
    projectId,
    freelancerId,
    freelancerFullName,
    assignedTotal,
    milestoneCount,
}: {
    projectId: string;
    freelancerId: string;
    freelancerFullName: string;
    assignedTotal: number | string;
    milestoneCount: number;
}) {
    const connection = useConnection();
    const walletQuery = useMyWallet();
    const createMutation = useCreateEscrow(projectId, freelancerId);

    const isWalletReady = Boolean(
        connection.address &&
            walletQuery.data?.isVerified &&
            walletQuery.data.walletAddress?.toLowerCase() ===
                connection.address.toLowerCase(),
    );

    return (
        <EscrowPanelShell
            title={`Escrow setup · ${freelancerFullName}`}
            statusLabel={
                isWalletReady ? "Wallet verified" : "Wallet required"
            }
            statusTone={isWalletReady ? "positive" : "warning"}
        >
            <p className="text-sm text-muted-foreground">
                This escrow pays {freelancerFullName} for{" "}
                {milestoneCount === 1
                    ? "1 milestone"
                    : `${milestoneCount} milestones`}
                , a total of {formatAmount(assignedTotal)}. Connect the
                client wallet that will fund it and prove ownership with
                a signature. Signing the verification message does not
                cost gas.
            </p>
            <p className="mt-3 text-sm text-muted-foreground">
                New escrows deduct a fixed 10% from each gross freelancer award,
                including case settlements. A 100-unit award pays the freelancer 90.
                You fund only the agreed total; client refunds have no fee.
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
                    <div className="rounded-xl border bg-background/35 px-4 py-3">
                        <p className="text-xs text-muted-foreground">
                            Payment network and token
                        </p>
                        <p className="mt-1 text-sm font-medium">
                            {ACTIVE_CHAIN_NAME} · {ACTIVE_TOKEN_SYMBOL}
                        </p>
                        <p className="mt-1 font-mono text-xs text-muted-foreground">
                            {truncateAddress(ACTIVE_TOKEN_ADDRESS)}
                        </p>
                    </div>

                    <Button
                        type="button"
                        disabled={!isWalletReady || createMutation.isPending}
                        onClick={() => {
                            createMutation.mutate({
                                freelancerId,
                                chainId: ACTIVE_CHAIN_ID,
                                tokenAddress: ACTIVE_TOKEN_ADDRESS,
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
    ownerName,
    viewerRole,
}: {
    projectId: string;
    escrow: ProjectEscrow;
    ownerName: string | null;
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

    const deployMutation = useDeployEscrow(
        projectId,
        escrow.freelancerId,
    );
    const syncMutation = useSyncEscrow(projectId, escrow.freelancerId);
    const startWorkMutation = useStartWork(
        projectId,
        escrow.freelancerId,
    );

    const workStartedQuery = useEscrowWorkStarted(escrow);

    return (
        <EscrowPanelShell
            title={ownerName ? `Escrow · ${ownerName}` : "Escrow"}
            statusLabel={formatStatusLabel(escrow.status)}
            statusTone={statusTone(escrow.status)}
        >
            <div className="grid gap-4 sm:grid-cols-3">
                <EscrowMeta
                    icon={CircleDollarSign}
                    label="Total"
                    value={formatAmount(moneyText(escrow, "totalAmount"))}
                />

                <EscrowMeta
                    icon={CheckCircle2}
                    label="Gross released"
                    value={formatAmount(moneyText(escrow, "releasedAmount"))}
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

            <p className="mt-4 text-sm text-muted-foreground">
                {(escrow.platformFeeBasisPoints ?? 0) === 1000
                    ? "A fixed 10% platform fee is deducted from each gross freelancer award, including case settlements. The client funds the agreed total; refunds have no fee."
                    : "This escrow uses the original terms: no platform fee."}
            </p>
            {(escrow.platformFeeBasisPoints ?? 0) === 1000 && (
                <div className="mt-3 grid gap-2 text-sm sm:grid-cols-2">
                    <p>Platform fees paid: {formatTokenPayment(moneyText(escrow, "platformFeesPaid"))} token units</p>
                    <p>Freelancer received: {formatTokenPayment(moneyText(escrow, "freelancerNetPaid"))} token units</p>
                    <p className="break-all sm:col-span-2">Fixed fee receiving wallet: {escrow.feeRecipientAddress}</p>
                </div>
            )}

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
                <div className="mt-5 border-t pt-5">
                    <div className="flex items-center gap-2 text-sm text-muted-foreground">
                        <LoaderCircle className="size-4 animate-spin" />
                        The deployment transaction is waiting for confirmation.
                    </div>
                    {viewerRole === "Client" && (
                        <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            className="mt-3"
                            disabled={deployMutation.isPending}
                            onClick={() => deployMutation.mutate()}
                        >
                            <RefreshCw
                                className={cn(
                                    "size-3.5",
                                    deployMutation.isPending && "animate-spin",
                                )}
                            />
                            Check deployment
                        </Button>
                    )}
                    {deployMutation.isError && (
                        <ErrorNotice
                            message={getApiErrorMessage(
                                deployMutation.error,
                                "Deployment status could not be checked.",
                            )}
                        />
                    )}
                </div>
            )}

            {escrow.status === "AwaitingFunding" && (
                <div className="mt-5 border-t pt-5">
                    {viewerRole === "Client" ? (
                        <>
                            <p className="mb-3 text-sm text-muted-foreground">
                                Fund this escrow with the total
                                for its milestones. Your wallet
                                approves the token transfer if
                                needed, then funds the contract.
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
                                                startWorkMutation.mutate({
                                                    contractAddress:
                                                        escrow.contractAddress,
                                                    chainId: escrow.chainId,
                                                });
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

                    {workStartedQuery.data === true && (
                        <p className="text-sm text-muted-foreground">
                            Escrow is funded and work has
                            started. Approve submitted milestones
                            to release payment.
                        </p>
                    )}

                    {viewerRole === "Freelancer" &&
                        workStartedQuery.data === true &&
                        isWalletVerified && (
                            <ResignControl
                                projectId={projectId}
                                escrow={escrow}
                            />
                        )}

                    {viewerRole === "Client" &&
                        workStartedQuery.data === false && (
                            <p className="text-sm text-muted-foreground">
                                Escrow is funded. The freelancer has not
                                started work on-chain yet, so you can still
                                cancel and get a refund.
                            </p>
                        )}
                </div>
            )}

            {(escrow.status === "AwaitingFunding" || escrow.status === "Funded") && (
                <div className="mt-5 border-t pt-5">
                    <p className="text-sm text-muted-foreground">
                        If your wallet confirmed a transaction but this page has
                        not updated, refresh its on-chain status. This does not
                        ask for a signature or move tokens. If a transaction is
                        still pending in your wallet, wait for it to finish.
                    </p>
                    <SyncButton
                        isPending={syncMutation.isPending}
                        onClick={() => syncMutation.mutate()}
                    />
                    {syncMutation.isError && (
                        <ErrorNotice message={getApiErrorMessage(syncMutation.error, "Escrow status could not be refreshed.")} />
                    )}
                </div>
            )}

            {viewerRole === "Client" &&
                escrow.contractAddress &&
                (escrow.status === "AwaitingFunding" ||
                    escrow.status === "Funded") &&
                workStartedQuery.data === false &&
                isWalletVerified && (
                    <CancelEscrowControl
                        projectId={projectId}
                        escrow={escrow}
                    />
                )}

            {escrow.contractAddress && (
                <details className="mt-5 border-t pt-5 text-sm">
                    <summary className="cursor-pointer font-medium">Transaction history</summary>
                    <dl className="mt-3 space-y-3 text-muted-foreground">
                        <div><dt>Deployment</dt><dd className="break-all">{escrow.deploymentTransactionHash ?? "Not recorded"}</dd></div>
                        <div><dt>Funding</dt><dd className="break-all">{escrow.fundingTransactionHash ?? "Not recorded"}</dd></div>
                        <div><dt>Cancellation</dt><dd className="break-all">{escrow.cancellationTransactionHash ?? "Not recorded"}</dd></div>
                    </dl>
                </details>
            )}

            {escrow.status === "Cancelled" && (
                <div className="mt-5 border-t pt-5 text-sm text-muted-foreground">
                    This escrow was cancelled before work started. Any funded
                    test tokens were returned to the client wallet.
                </div>
            )}

            {escrow.status === "Completed" && (
                <div className="mt-5 border-t pt-5 text-sm text-muted-foreground">
                    All milestones have been released. The escrow
                    is complete.
                </div>
            )}

            {escrow.status === "Terminated" && (
                <div className="mt-5 border-t pt-5 text-sm text-muted-foreground">
                    This escrow ended early, either because the freelancer
                    left or because a reviewer settled a case and ended it.
                    Milestones already paid stay paid. The rest went back to
                    the client, and the unpaid milestones are open for hire
                    again.
                </div>
            )}
        </EscrowPanelShell>
    );
}

function ResignControl({
    projectId,
    escrow,
}: {
    projectId: string;
    escrow: ProjectEscrow;
}) {
    const action = useCaseAction(projectId, escrow.freelancerId);

    return (
        <div className="mt-4">
            <AlertDialog>
                <AlertDialogTrigger asChild>
                    <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={action.isPending}
                    >
                        {action.isPending && (
                            <LoaderCircle className="size-4 animate-spin" />
                        )}
                        Leave this project
                    </Button>
                </AlertDialogTrigger>

                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>
                            Leave this project?
                        </AlertDialogTitle>

                        <AlertDialogDescription>
                            You give up all milestones that are not paid yet.
                            Everything unpaid in this escrow goes back to the
                            client, and those milestones are open for other
                            freelancers. Milestones already paid stay yours.
                            This cannot be undone.
                        </AlertDialogDescription>
                    </AlertDialogHeader>

                    <AlertDialogFooter>
                        <AlertDialogCancel>Stay</AlertDialogCancel>

                        <AlertDialogAction
                            onClick={() =>
                                action.mutate({ escrow, action: "resign" })
                            }
                        >
                            Leave project
                        </AlertDialogAction>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>
        </div>
    );
}

function CancelEscrowControl({
    projectId,
    escrow,
}: {
    projectId: string;
    escrow: ProjectEscrow;
}) {
    const cancelMutation = useCancelEscrow(
        projectId,
        escrow.freelancerId,
    );

    return (
        <div className="mt-5 border-t pt-5">
            <AlertDialog>
                <AlertDialogTrigger asChild>
                    <Button
                        type="button"
                        variant="outline"
                        disabled={cancelMutation.isPending}
                    >
                        <Ban className="size-4" />
                        Cancel escrow
                    </Button>
                </AlertDialogTrigger>
                <AlertDialogContent>
                    <AlertDialogHeader>
                        <AlertDialogTitle>
                            Cancel this escrow?
                        </AlertDialogTitle>
                        <AlertDialogDescription>
                            You can cancel only before the freelancer starts
                            work. If the escrow is funded, the contract returns
                            the full token balance to your wallet. This
                            freelancer leaves the project, and the stages they
                            were going to deliver go back on the market so you
                            can hire someone else for them.
                        </AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                        <AlertDialogCancel disabled={cancelMutation.isPending}>
                            Keep escrow
                        </AlertDialogCancel>
                        <Button
                            type="button"
                            variant="destructive"
                            disabled={cancelMutation.isPending}
                            onClick={() => cancelMutation.mutate(escrow)}
                        >
                            {cancelMutation.isPending ? (
                                <LoaderCircle className="size-4 animate-spin" />
                            ) : (
                                <Ban className="size-4" />
                            )}
                            Cancel and refund
                        </Button>
                    </AlertDialogFooter>
                </AlertDialogContent>
            </AlertDialog>

            {cancelMutation.isError && (
                <ErrorNotice
                    message={getWalletErrorMessage(
                        cancelMutation.error,
                        "Escrow cancellation failed.",
                    )}
                />
            )}
        </div>
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
    const fundMutation = useFundEscrow(projectId, escrow.freelancerId);
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
        chainId: escrow.chainId,
        query: {
            enabled: checksEnabled,
            staleTime: Number.POSITIVE_INFINITY,
        },
    });

    const contractAmountQuery = useReadContract({
        address: escrow.contractAddress as Address,
        abi: escrowAbi,
        functionName: "totalAmount",
        chainId: escrow.chainId,
        query: { enabled: checksEnabled && Boolean(escrow.contractAddress) },
    });

    const tokenBalanceQuery = useReadContract({
        address: escrow.tokenAddress as Address,
        abi: erc20Abi,
        functionName: "balanceOf",
        args: [connection.address ?? zeroAddress],
        chainId: escrow.chainId,
        query: {
            enabled: checksEnabled,
            refetchInterval: 5_000,
        },
    });

    const gasBalanceQuery = useBalance({
        address: connection.address,
        chainId: escrow.chainId,
        query: {
            enabled: checksEnabled,
            refetchInterval: 5_000,
        },
    });

    const tokenDecimals = tokenDecimalsQuery.data;
    const unsupportedDecimals = tokenDecimals !== undefined && tokenDecimals > 18;
    const requiredTokenAmount = unsupportedDecimals
        ? null
        : contractAmountQuery.data ?? null;

    const tokenBalance = tokenBalanceQuery.data ?? 0n;
    const gasBalance = gasBalanceQuery.data?.value ?? 0n;

    const tokenShortfall =
        requiredTokenAmount === null ||
        tokenBalance >= requiredTokenAmount
            ? 0n
            : requiredTokenAmount - tokenBalance;

    const networkReady =
        escrow.chainId === ACTIVE_CHAIN_ID &&
        connection.chainId === escrow.chainId;

    const tokenReady =
        requiredTokenAmount !== null &&
        tokenBalanceQuery.isSuccess &&
        tokenBalance >= requiredTokenAmount;

    const gasReady =
        gasBalanceQuery.isSuccess &&
        gasBalance >= MIN_GAS_BALANCE;

    const checksLoading =
        tokenDecimalsQuery.isLoading ||
        contractAmountQuery.isLoading ||
        tokenBalanceQuery.isLoading ||
        gasBalanceQuery.isLoading;

    const canFund =
        networkReady &&
        tokenReady &&
        gasReady &&
        tokenDecimalsQuery.isSuccess &&
        contractAmountQuery.isSuccess &&
        !unsupportedDecimals &&
        !checksLoading;

    const testTokenMintAvailable =
        TEST_TOKEN_MINT_ENABLED &&
        escrow.chainId === ACTIVE_CHAIN_ID &&
        escrow.tokenAddress.toLowerCase() ===
            ACTIVE_TOKEN_ADDRESS.toLowerCase();

    const balanceReadFailed =
        tokenDecimalsQuery.isError ||
        contractAmountQuery.isError ||
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
                            ? ACTIVE_CHAIN_NAME
                            : escrow.chainId === ACTIVE_CHAIN_ID
                              ? `Switch to ${ACTIVE_CHAIN_NAME}`
                              : `Unsupported chain ${escrow.chainId}`
                    }
                    ready={networkReady}
                />

                <FundingReadinessItem
                    icon={Coins}
                    label={ACTIVE_TOKEN_SYMBOL}
                    value={formatTokenFundingBalance(
                        tokenBalance,
                        tokenDecimals,
                        requiredTokenAmount,
                        tokenBalanceQuery.isLoading ||
                            tokenDecimalsQuery.isLoading || contractAmountQuery.isLoading,
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
                <ErrorNotice message="Wallet balances or the exact escrow total could not be read from the configured network. Funding is unavailable until they can be checked." />
            )}
            {unsupportedDecimals && <ErrorNotice message="This token uses unsupported precision. Tokens with 0 to 18 decimals are supported." />}

            <div className="flex flex-wrap gap-3">
                {!networkReady &&
                    escrow.chainId === ACTIVE_CHAIN_ID && (
                        <Button
                            type="button"
                            variant="outline"
                            disabled={switchChainMutation.isPending}
                            onClick={() => {
                                switchChainMutation.mutate({
                                    chainId: escrow.chainId,
                                });
                            }}
                        >
                            {switchChainMutation.isPending ? (
                                <LoaderCircle className="size-4 animate-spin" />
                            ) : (
                                <Network className="size-4" />
                            )}
                            Switch network
                        </Button>
                    )}

                {testTokenMintAvailable &&
                    networkReady &&
                    (!tokenReady || !gasReady) && (
                        <Button
                            type="button"
                            variant="outline"
                            disabled={
                                checksLoading ||
                                balanceReadFailed || unsupportedDecimals ||
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
                    {ACTIVE_CHAIN_NAME} and holds this escrow’s total plus gas.
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
                        "Test funds could not be prepared.",
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



function formatTokenFundingBalance(
    balance: bigint,
    decimals: number | undefined,
    requiredAmount: bigint | null,
    loading: boolean,
) {
    if (loading || decimals === undefined) {
        return "Checking balance";
    }

    if (decimals > 18) return "Unsupported token precision";
    const required = requiredAmount === null ? "Unavailable" : formatBaseUnits(requiredAmount, decimals);
    return `${formatBaseUnits(balance, decimals)} / ${required} mUSDC`;
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
