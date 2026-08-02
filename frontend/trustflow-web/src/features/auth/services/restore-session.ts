import "client-only";

import { cancelRefreshSession, refreshSession } from "@/lib/api/api-client";
import { isUnauthorizedError } from "@/lib/api/is-unauthorized-error";
import { useAuthStore } from "@/stores/auth-store";

const RESTORE_TIMEOUT_MS = 10_000;

let restorePromise: Promise<void> | null = null;

export function restoreSession() {
  if (restorePromise) {
    return restorePromise;
  }

  restorePromise = runRestoreSession().finally(() => {
    restorePromise = null;
  });

  return restorePromise;
}

async function runRestoreSession() {
  const authStore = useAuthStore.getState();

  /*
   * Agar session ghablan restore shode,
   * request-e ezafi nemifrestim.
   */
  if (authStore.status === "authenticated") {
    return;
  }

  authStore.setStatus("checking");

  const timeoutId = window.setTimeout(() => {
    cancelRefreshSession();
  }, RESTORE_TIMEOUT_MS);

  try {
    await refreshSession();
  } catch (error) {
    const latestAuthState = useAuthStore.getState();

    /*
     * Momkene hamzaman login movafagh
     * shode bashe. Dar in halat natije-e
     * request-e ghadimi nabayad session-e
     * jadid ro kharab kone.
     */
    if (latestAuthState.status === "authenticated") {
      return;
    }

    if (isUnauthorizedError(error)) {
      latestAuthState.clearSession();
    } else {
      latestAuthState.setStatus("unavailable");
    }

    throw error;
  } finally {
    window.clearTimeout(timeoutId);

    const latestAuthState = useAuthStore.getState();

    /*
     * Safety net:
     * status hichvaght nabayad rooye
     * checking bemone.
     */
    if (latestAuthState.status === "checking") {
      latestAuthState.setStatus("unavailable");
    }
  }
}
