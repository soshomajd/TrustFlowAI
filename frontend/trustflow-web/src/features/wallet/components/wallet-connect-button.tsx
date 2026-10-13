"use client";

import { CheckCircle2, LoaderCircle, Unplug, Wallet } from "lucide-react";
import { useConnection } from "wagmi";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useConnectAndVerifyWallet } from "@/features/wallet/hooks/use-connect-and-verify-wallet";
import { useDisconnectWallet } from "@/features/wallet/hooks/use-disconnect-wallet";
import { useMyWallet } from "@/features/wallet/hooks/use-my-wallet";

export function WalletConnectButton() {
  const connection = useConnection();
  const walletQuery = useMyWallet();

  const connectMutation = useConnectAndVerifyWallet();
  const disconnectMutation = useDisconnectWallet();

  const isVerified = Boolean(
    connection.address &&
      walletQuery.data?.isVerified &&
      walletQuery.data.walletAddress?.toLowerCase() ===
        connection.address.toLowerCase(),
  );

  const connectLabel = walletQuery.data?.isVerified
    ? "Connect verified wallet"
    : connection.isConnected
      ? "Verify wallet"
      : "Connect wallet";

  return (
    <div className="flex items-center gap-2">
      {connection.isConnected && isVerified ? (
        <Badge
          variant="outline"
          className="border-emerald-500/30 bg-emerald-500/10 text-emerald-400"
        >
          <CheckCircle2 className="size-3.5" />
          {truncateAddress(connection.address)}
        </Badge>
      ) : (
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-label={connectLabel}
          disabled={connectMutation.isPending || walletQuery.isLoading}
          onClick={() => {
            connectMutation.mutate();
          }}
        >
          {connectMutation.isPending ? (
            <LoaderCircle className="size-4 animate-spin" />
          ) : (
            <Wallet className="size-4" />
          )}

          <span className="hidden sm:inline">{connectLabel}</span>
        </Button>
      )}

      {connection.isConnected && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={disconnectMutation.isPending}
          onClick={() => {
            disconnectMutation.mutate();
          }}
          title="Disconnect this wallet from TrustFlow. Your account keeps its verified address."
          aria-label="Disconnect wallet"
        >
          {disconnectMutation.isPending ? (
            <LoaderCircle className="size-4 animate-spin" />
          ) : (
            <Unplug className="size-4" />
          )}

          <span className="hidden sm:inline">Disconnect</span>
        </Button>
      )}
    </div>
  );
}

function truncateAddress(address: string | undefined) {
  if (!address) {
    return "";
  }

  return `${address.slice(0, 6)}...${address.slice(-4)}`;
}
