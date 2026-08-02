"use client";

import {
    Filter,
    RotateCcw,
} from "lucide-react";

import { Button } from
    "@/components/ui/button";
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select";
import { useClientProjectsSearchParams } from
    "@/features/projects/hooks/use-client-projects-search-params";
import type { ProjectStatus } from
    "@/features/projects/types/project";

export function ClientProjectsFilters() {
    const {
        page,
        status,
        setStatus,
        resetSearchParams,
        hasUrlState,
    } = useClientProjectsSearchParams();

    function handleStatusChange(
        value: string,
    ) {
        const nextStatus =
            value === "all"
                ? undefined
                : (value as ProjectStatus);

        setStatus(nextStatus);
    }

    return (
        <section className="mt-8 flex flex-col gap-4 rounded-2xl border bg-card/70 p-4 backdrop-blur sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
                <div className="flex items-center gap-2 text-sm font-medium">
                    <Filter className="size-4 text-electric" />

                    Filter projects
                </div>

                <Select
                    value={status ?? "all"}
                    onValueChange={
                        handleStatusChange
                    }
                >
                    <SelectTrigger className="w-full sm:w-48">
                        <SelectValue placeholder="All statuses" />
                    </SelectTrigger>

                    <SelectContent>
                        <SelectItem value="all">
                            All statuses
                        </SelectItem>

                        <SelectItem value="Open">
                            Open
                        </SelectItem>

                        <SelectItem value="InProgress">
                            In progress
                        </SelectItem>

                        <SelectItem value="Completed">
                            Completed
                        </SelectItem>

                        <SelectItem value="Cancelled">
                            Cancelled
                        </SelectItem>
                    </SelectContent>
                </Select>
            </div>

            <div className="flex items-center justify-between gap-3 sm:justify-end">
                <p className="text-xs text-muted-foreground">
                    Page {page}
                </p>

                {hasUrlState && (
                    <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={resetSearchParams}
                    >
                        <RotateCcw className="size-4" />
                        Reset
                    </Button>
                )}
            </div>
        </section>
    );
}