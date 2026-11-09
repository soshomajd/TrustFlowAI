import type { Hash } from "viem";

// Keep submitted hashes across retries/rerenders in this page, never wallet keys.
// A receipt timeout is not evidence that a transaction failed. Reuse its hash.
export function createTransactionConfirmer() {
  const submitted = new Map<string, Hash>();
  const running = new Map<string, Promise<void>>();

  return function confirmTransaction(
    key: string,
    send: () => Promise<Hash>,
    wait: (hash: Hash) => Promise<{ status: "success" | "reverted" }>,
  ): Promise<void> {
    const existing = running.get(key);
    if (existing) return existing;

    const operation = (async () => {
      let hash = submitted.get(key);
      const storageKey = `trustflow:pending-transaction:v1:${key}`;
      if (!hash && typeof window !== "undefined") {
        try {
          const stored = window.localStorage.getItem(storageKey);
          if (stored && /^0x[0-9a-fA-F]{64}$/.test(stored)) hash = stored as Hash;
        } catch { /* Wallet confirmation remains usable when storage is disabled. */ }
      }
      if (!hash) {
        hash = await send();
        submitted.set(key, hash);
        if (typeof window !== "undefined") {
          try { window.localStorage.setItem(storageKey, hash); } catch { /* No secrets are stored. */ }
        }
      }

      let receipt;
      try {
        receipt = await wait(hash);
      } catch {
        throw new Error("A transaction was submitted, but its confirmation could not be read. Wait for your wallet to finish, then use Refresh on-chain status or retry to check the same transaction.");
      }
      submitted.delete(key);
      if (typeof window !== "undefined") {
        try { window.localStorage.removeItem(storageKey); } catch { /* Best effort cleanup. */ }
      }
      if (receipt.status !== "success") {
        throw new Error("The transaction reverted. Refresh the on-chain status before trying again.");
      }
    })();
    running.set(key, operation);
    void operation.finally(() => running.delete(key)).catch(() => {});
    return operation;
  };
}

export const confirmEscrowTransaction = createTransactionConfirmer();
