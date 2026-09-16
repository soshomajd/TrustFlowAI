"use client";

import { CheckCircle2, LoaderCircle, Wallet } from "lucide-react";
import { useConnection } from "wagmi";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useConnectAndVerifyWallet } from "@/features/wallet/hooks/use-connect-and-verify-wallet";
import { useMyWallet } from "@/features/wallet/hooks/use-my-wallet";

export function WalletConnectButton() {
  const connection = useConnection();
  const walletQuery = useMyWallet();

  const connectMutation = useConnectAndVerifyWallet();

  const isVerified = Boolean(
    connection.address &&
      walletQuery.data?.isVerified &&
      walletQuery.data.walletAddress?.toLowerCase() ===
        connection.address.toLowerCase(),
  );

  if (connection.isConnected && isVerified) {
    return (
      <Badge
        variant="outline"
        className="border-emerald-500/30 bg-emerald-500/10 text-emerald-400"
      >
        <CheckCircle2 className="size-3.5" />
        {truncateAddress(connection.address)}
      </Badge>
    );
  }

  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
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

      {walletQuery.data?.isVerified
        ? "Connect verified wallet"
        : connection.isConnected
          ? "Verify wallet"
          : "Connect wallet"}
    </Button>
  );
}

function truncateAddress(address: string | undefined) {
  if (!address) {
    return "";
  }

  return `${address.slice(0, 6)}...${address.slice(-4)}`;
}
