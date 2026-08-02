"use client";

import { useCallback } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

import type { ProjectStatus } from "@/features/projects/types/project";

const PROJECT_STATUSES: ProjectStatus[] = [
  "Open",
  "InProgress",
  "Completed",
  "Cancelled",
];

export function useClientProjectsSearchParams() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const page = parsePage(searchParams.get("page"));

  const status = parseProjectStatus(searchParams.get("status"));

  const updateSearchParams = useCallback(
    (update: (params: URLSearchParams) => void) => {
      const params = new URLSearchParams(searchParams.toString());

      update(params);

      const queryString = params.toString();

      const nextUrl = queryString ? `${pathname}?${queryString}` : pathname;

      router.replace(nextUrl, {
        scroll: false,
      });
    },
    [pathname, router, searchParams],
  );

  const setStatus = useCallback(
    (nextStatus: ProjectStatus | undefined) => {
      updateSearchParams((params) => {
        if (nextStatus) {
          params.set("status", nextStatus);
        } else {
          params.delete("status");
        }

        /*
         * Vaghti filter taghir mikone,
         * bayad dobare az page 1 shoroo konim.
         */
        params.delete("page");
      });
    },
    [updateSearchParams],
  );

  const setPage = useCallback(
    (nextPage: number) => {
      updateSearchParams((params) => {
        if (nextPage <= 1) {
          params.delete("page");
        } else {
          params.set("page", String(nextPage));
        }
      });
    },
    [updateSearchParams],
  );

  const resetSearchParams = useCallback(() => {
    updateSearchParams((params) => {
      params.delete("status");
      params.delete("page");
    });
  }, [updateSearchParams]);

  return {
    page,
    status,

    setPage,
    setStatus,
    resetSearchParams,

    hasUrlState: Boolean(status) || page > 1,
  };
}

function parsePage(value: string | null) {
  if (!value) {
    return 1;
  }

  const parsedPage = Number.parseInt(value, 10);

  if (!Number.isInteger(parsedPage) || parsedPage < 1) {
    return 1;
  }

  return parsedPage;
}

function parseProjectStatus(value: string | null): ProjectStatus | undefined {
  if (!value) {
    return undefined;
  }

  return PROJECT_STATUSES.find((status) => status === value);
}
