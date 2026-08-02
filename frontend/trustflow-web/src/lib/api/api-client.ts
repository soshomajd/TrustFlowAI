import "client-only";

import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";
import { isUnauthorizedError } from "@/lib/api/is-unauthorized-error";

import { env } from "@/config/env";
import type { AuthSessionResponse } from "@/features/auth/types/auth";
import { useAuthStore } from "@/stores/auth-store";

type RetryableRequestConfig = InternalAxiosRequestConfig & {
  _retry?: boolean;
};

const axiosDefaults = {
  baseURL: env.apiUrl,
  timeout: 15_000,
  withCredentials: true,
  headers: {
    Accept: "application/json",
  },
} as const;

export const apiClient = axios.create(axiosDefaults);

/*
 * In client interceptor nadare.
 * Faghat baraye refresh-token estefade mishe
 * ta loop-e 401 ijad nashe.
 */
const refreshClient = axios.create(axiosDefaults);
const AUTHENTICATION_PATHS = [
  "/auth/login",
  "/auth/register",
  "/auth/refresh",
  "/auth/logout",
] as const;

function isAuthenticationRequest(requestUrl?: string) {
  if (!requestUrl) {
    return false;
  }

  return AUTHENTICATION_PATHS.some((path) => requestUrl.includes(path));
}

let refreshPromise: Promise<AuthSessionResponse> | null = null;

let refreshAbortController: AbortController | null = null;

async function requestRefreshSession(signal: AbortSignal) {
  const response = await refreshClient.post<AuthSessionResponse>(
    "/auth/refresh",
    undefined,
    {
      signal,
    },
  );

  useAuthStore.getState().setSession(response.data);

  return response.data;
}

export function refreshSession() {
  if (refreshPromise) {
    return refreshPromise;
  }

  const controller = new AbortController();

  refreshAbortController = controller;

  refreshPromise = requestRefreshSession(controller.signal).finally(() => {
    refreshPromise = null;

    if (refreshAbortController === controller) {
      refreshAbortController = null;
    }
  });

  return refreshPromise;
}

export function cancelRefreshSession() {
  refreshAbortController?.abort();
}

apiClient.interceptors.request.use(
  (config) => {
    const accessToken = useAuthStore.getState().accessToken;

    if (accessToken) {
      config.headers.Authorization = `Bearer ${accessToken}`;
    }

    return config;
  },
  (error: unknown) => Promise.reject(error),
);

apiClient.interceptors.response.use(
  (response) => response,

  async (error: AxiosError) => {
    const statusCode = error.response?.status;

    const originalRequest = error.config as RetryableRequestConfig | undefined;

    const cannotRefresh =
      statusCode !== 401 ||
      !originalRequest ||
      originalRequest._retry ||
      isAuthenticationRequest(originalRequest.url);

    if (cannotRefresh) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    try {
      const session = await refreshSession();

      originalRequest.headers.Authorization = `Bearer ${session.accessToken}`;

      return apiClient(originalRequest);
    } catch (refreshError) {
      if (isUnauthorizedError(refreshError)) {
        useAuthStore.getState().clearSession();
      }

      return Promise.reject(refreshError);
    }
  },
);
