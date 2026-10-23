import { create } from "zustand";

import type { AuthSessionResponse, AuthUser } from "@/features/auth/types/auth";

export type AuthStatus =
  | "idle"
  | "checking"
  | "authenticated"
  | "unauthenticated"
  | "unavailable";

type AuthState = {
  readOnly: boolean;
  accessToken: string | null;
  expiresAtUtc: string | null;
  user: AuthUser | null;
  status: AuthStatus;

  setStatus: (status: AuthStatus) => void;

  setSession: (session: AuthSessionResponse) => void;

  clearSession: () => void;
};

export const useAuthStore = create<AuthState>()((set) => ({
  readOnly: false,
  accessToken: null,
  expiresAtUtc: null,
  user: null,
  status: "idle",

  setStatus: (status) => {
    set({ status });
  },

  setSession: (session) => {
    set({
      accessToken: session.accessToken,
      expiresAtUtc: session.expiresAtUtc,
      user: session.user,
      readOnly: session.readOnly === true,
      status: "authenticated",
    });
  },

  clearSession: () => {
    set({
      accessToken: null,
      expiresAtUtc: null,
      user: null,
      readOnly: false,
      status: "unauthenticated",
    });
  },
}));

export const selectIsAuthenticated = (state: AuthState) =>
  state.status === "authenticated" &&
  Boolean(state.accessToken) &&
  Boolean(state.user);
