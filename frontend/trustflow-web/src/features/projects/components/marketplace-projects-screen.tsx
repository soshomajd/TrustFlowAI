"use client";

import {
    BriefcaseBusiness,
    CalendarDays,
    ChevronLeft,
    ChevronRight,
    CircleDollarSign,
    Clock3,
    FilterX,
    LoaderCircle,
    RefreshCw,
    Search,
    TriangleAlert,
} from "lucide-react";
import Link from "next/link";
import {
    usePathname,
    useRouter,
    useSearchParams,
} from "next/navigation";
import type {
    FormEvent,
} from "react";
import { useState } from "react";

import {
    AnimatedSection,
    StaggerContainer,
    StaggerItem,
} from "@/components/motion/animation-primitives";
import { Button } from
    "@/components/ui/button";
import { Input } from
    "@/components/ui/input";
import { Label } from
    "@/components/ui/label";
import { useMarketplaceProjects } from
    "@/features/projects/hooks/use-marketplace-projects";
import type {
    MarketplaceProject,
    ProjectMarketplaceSortOption,
} from
    "@/features/projects/types/marketplace-project";
import { getApiErrorMessage } from
    "@/lib/api/get-api-error-message";

const PAGE_SIZE = 12;

const sortOptions: Array<{
    value:
    ProjectMarketplaceSortOption;
    label: string;
}> = [
        {
            value: "Newest",
            label: "Newest first",
        },
        {
            value: "Oldest",
            label: "Oldest first",
        },
        {
            value: "BudgetLowToHigh",
            label: "Budget: low to high",
        },
        {
            value: "BudgetHighToLow",
            label: "Budget: high to low",
        },
        {
            value: "DeadlineSoonest",
            label: "Deadline: soonest",
        },
        {
            value: "DeadlineLatest",
            label: "Deadline: latest",
        },
    ];

export function MarketplaceProjectsScreen() {
    const router = useRouter();
    const pathname = usePathname();
    const searchParams =
        useSearchParams();

    const [filterError, setFilterError] =
        useState<string | null>(null);

    const page = parsePage(
        searchParams.get("page"),
    );

    const search =
        searchParams.get("search") ??
        undefined;

    const minBudget =
        parseBudgetFromUrl(
            searchParams.get("minBudget"),
        );

    const maxBudget =
        parseBudgetFromUrl(
            searchParams.get("maxBudget"),
        );

    const deadlineBefore =
        searchParams.get(
            "deadlineBefore",
        ) ?? undefined;

    const sortBy = parseSortOption(
        searchParams.get("sortBy"),
    );

    const projectsQuery =
        useMarketplaceProjects({
            page,
            pageSize: PAGE_SIZE,
            search,
            minBudget,
            maxBudget,
            deadlineBefore:
                deadlineBefore
                    ? toEndOfDayIso(
                        deadlineBefore,
                    )
                    : undefined,
            sortBy,
        });

    function handleFilterSubmit(
        event:
            FormEvent<HTMLFormElement>,
    ) {
        event.preventDefault();

        const formData =
            new FormData(
                event.currentTarget,
            );

        const nextSearch = String(
            formData.get("search") ?? "",
        ).trim();

        const minBudgetResult =
            parseBudgetInput(
                String(
                    formData.get(
                        "minBudget",
                    ) ?? "",
                ),
            );

        const maxBudgetResult =
            parseBudgetInput(
                String(
                    formData.get(
                        "maxBudget",
                    ) ?? "",
                ),
            );

        const nextDeadline =
            String(
                formData.get(
                    "deadlineBefore",
                ) ?? "",
            ).trim();

        const nextSortBy =
            parseSortOption(
                String(
                    formData.get(
                        "sortBy",
                    ) ?? "",
                ),
            );

        if (
            minBudgetResult === null ||
            maxBudgetResult === null
        ) {
            setFilterError(
                "Budget must be a valid positive number with at most 2 decimal places.",
            );

            return;
        }

        if (
            minBudgetResult !==
            undefined &&
            maxBudgetResult !==
            undefined &&
            minBudgetResult >
            maxBudgetResult
        ) {
            setFilterError(
                "Minimum budget cannot be greater than maximum budget.",
            );

            return;
        }

        setFilterError(null);

        const nextParams =
            new URLSearchParams();

        if (nextSearch) {
            nextParams.set(
                "search",
                nextSearch,
            );
        }

        if (
            minBudgetResult !==
            undefined
        ) {
            nextParams.set(
                "minBudget",
                String(minBudgetResult),
            );
        }

        if (
            maxBudgetResult !==
            undefined
        ) {
            nextParams.set(
                "maxBudget",
                String(maxBudgetResult),
            );
        }

        if (nextDeadline) {
            nextParams.set(
                "deadlineBefore",
                nextDeadline,
            );
        }

        if (
            nextSortBy !== "Newest"
        ) {
            nextParams.set(
                "sortBy",
                nextSortBy,
            );
        }

        router.replace(
            createUrl(
                pathname,
                nextParams,
            ),
            {
                scroll: false,
            },
        );
    }

    function handleClearFilters() {
        setFilterError(null);

        router.replace(pathname, {
            scroll: false,
        });
    }

    function changePage(
        nextPage: number,
    ) {
        const nextParams =
            new URLSearchParams(
                searchParams.toString(),
            );

        if (nextPage <= 1) {
            nextParams.delete("page");
        } else {
            nextParams.set(
                "page",
                String(nextPage),
            );
        }

        router.replace(
            createUrl(
                pathname,
                nextParams,
            ),
            {
                scroll: false,
            },
        );
    }

    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <AnimatedSection>
                <div className="rounded-2xl border bg-card/80 p-6 shadow-blue-glow backdrop-blur sm:p-8">
                    <div className="flex size-12 items-center justify-center rounded-xl bg-primary/10">
                        <BriefcaseBusiness className="size-6 text-electric" />
                    </div>

                    <p className="mt-6 text-sm font-medium uppercase tracking-[0.25em] text-electric">
                        Freelancer marketplace
                    </p>

                    <h1 className="mt-3 text-3xl font-semibold">
                        Find your next project
                    </h1>

                    <p className="mt-3 max-w-2xl text-muted-foreground">
                        Browse open projects,
                        compare budgets and submit
                        a proposal for work that
                        matches your experience.
                    </p>
                </div>
            </AnimatedSection>

            <MarketplaceFilters
                key={searchParams.toString()}
                search={search ?? ""}
                minBudget={
                    searchParams.get(
                        "minBudget",
                    ) ?? ""
                }
                maxBudget={
                    searchParams.get(
                        "maxBudget",
                    ) ?? ""
                }
                deadlineBefore={
                    deadlineBefore ?? ""
                }
                sortBy={sortBy}
                error={filterError}
                onSubmit={
                    handleFilterSubmit
                }
                onClear={
                    handleClearFilters
                }
            />

            <MarketplaceResults
                query={projectsQuery}
                onPrevious={() => {
                    changePage(page - 1);
                }}
                onNext={() => {
                    changePage(page + 1);
                }}
            />
        </main>
    );
}

type MarketplaceFiltersProps = {
    search: string;
    minBudget: string;
    maxBudget: string;
    deadlineBefore: string;

    sortBy:
    ProjectMarketplaceSortOption;

    error: string | null;

    onSubmit: (
        event:
            FormEvent<HTMLFormElement>,
    ) => void;

    onClear: () => void;
};

function MarketplaceFilters({
    search,
    minBudget,
    maxBudget,
    deadlineBefore,
    sortBy,
    error,
    onSubmit,
    onClear,
}: MarketplaceFiltersProps) {
    return (
        <AnimatedSection
            className="mt-6"
        >
            <form
                noValidate
                onSubmit={onSubmit}
                className="rounded-2xl border bg-card/70 p-5 backdrop-blur sm:p-6"
            >
                <div className="grid gap-4 lg:grid-cols-6">
                    <div className="space-y-2 lg:col-span-2">
                        <Label htmlFor="marketplace-search">
                            Search
                        </Label>

                        <div className="relative">
                            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />

                            <Input
                                id="marketplace-search"
                                name="search"
                                type="search"
                                maxLength={100}
                                defaultValue={search}
                                placeholder="Search projects"
                                className="pl-10"
                            />
                        </div>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="marketplace-min-budget">
                            Min budget
                        </Label>

                        <Input
                            id="marketplace-min-budget"
                            name="minBudget"
                            type="text"
                            inputMode="decimal"
                            defaultValue={minBudget}
                            placeholder="0"
                        />
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="marketplace-max-budget">
                            Max budget
                        </Label>

                        <Input
                            id="marketplace-max-budget"
                            name="maxBudget"
                            type="text"
                            inputMode="decimal"
                            defaultValue={maxBudget}
                            placeholder="10000"
                        />
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="marketplace-deadline">
                            Deadline before
                        </Label>

                        <Input
                            id="marketplace-deadline"
                            name="deadlineBefore"
                            type="date"
                            defaultValue={
                                deadlineBefore
                            }
                        />
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="marketplace-sort">
                            Sort by
                        </Label>

                        <select
                            id="marketplace-sort"
                            name="sortBy"
                            defaultValue={sortBy}
                            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs outline-none transition focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
                        >
                            {sortOptions.map(
                                (option) => (
                                    <option
                                        key={option.value}
                                        value={
                                            option.value
                                        }
                                        className="bg-background"
                                    >
                                        {option.label}
                                    </option>
                                ),
                            )}
                        </select>
                    </div>
                </div>

                {error && (
                    <p className="mt-4 text-sm text-destructive">
                        {error}
                    </p>
                )}

                <div className="mt-5 flex flex-col gap-3 border-t pt-5 sm:flex-row sm:justify-end">
                    <Button
                        type="button"
                        variant="outline"
                        onClick={onClear}
                    >
                        <FilterX className="size-4" />
                        Clear filters
                    </Button>

                    <Button type="submit">
                        <Search className="size-4" />
                        Apply filters
                    </Button>
                </div>
            </form>
        </AnimatedSection>
    );
}

type MarketplaceResultsProps = {
    query:
    ReturnType<
        typeof useMarketplaceProjects
    >;

    onPrevious: () => void;
    onNext: () => void;
};

function MarketplaceResults({
    query,
    onPrevious,
    onNext,
}: MarketplaceResultsProps) {
    if (query.isLoading) {
        return (
            <MarketplaceResultsSkeleton />
        );
    }

    if (query.isError) {
        return (
            <MarketplaceError
                message={getApiErrorMessage(
                    query.error,
                    "Marketplace projects could not be loaded.",
                )}
                onRetry={() => {
                    void query.refetch();
                }}
            />
        );
    }

    const response = query.data;

    if (
        !response ||
        response.items.length === 0
    ) {
        return <MarketplaceEmpty />;
    }

    return (
        <AnimatedSection
            className="mt-6"
        >
            <section
                aria-labelledby="marketplace-results-title"
                className="rounded-2xl border bg-card/50 p-5 sm:p-6"
            >
                <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                        <h2
                            id="marketplace-results-title"
                            className="text-xl font-semibold"
                        >
                            Open projects
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            {response.totalItems}{" "}
                            {response.totalItems === 1
                                ? "project"
                                : "projects"}{" "}
                            found
                        </p>
                    </div>

                    {query.isFetching && (
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            <LoaderCircle className="size-4 animate-spin" />
                            Updating results
                        </div>
                    )}
                </header>

                <StaggerContainer className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    {response.items.map(
                        (project) => (
                            <StaggerItem
                                key={project.id}
                                className="h-full"
                            >
                                <MarketplaceProjectCard
                                    project={project}
                                />
                            </StaggerItem>
                        ),
                    )}
                </StaggerContainer>

                {response.totalPages > 1 && (
                    <div className="mt-6 flex flex-col gap-3 border-t pt-5 sm:flex-row sm:items-center sm:justify-between">
                        <p className="text-sm text-muted-foreground">
                            Page {response.page} of{" "}
                            {response.totalPages}
                        </p>

                        <div className="flex items-center gap-2">
                            <Button
                                type="button"
                                variant="outline"
                                disabled={
                                    !response.hasPreviousPage ||
                                    query.isFetching
                                }
                                onClick={onPrevious}
                            >
                                <ChevronLeft className="size-4" />
                                Previous
                            </Button>

                            <Button
                                type="button"
                                variant="outline"
                                disabled={
                                    !response.hasNextPage ||
                                    query.isFetching
                                }
                                onClick={onNext}
                            >
                                Next
                                <ChevronRight className="size-4" />
                            </Button>
                        </div>
                    </div>
                )}
            </section>
        </AnimatedSection>
    );
}

function MarketplaceProjectCard({
    project,
}: {
    project: MarketplaceProject;
}) {
    const remainingBudget =
        Math.max(
            0,
            project.budget -
            project.allocatedAmount,
        );

    return (
        <article className="flex h-full flex-col rounded-xl border bg-background/40 p-5 transition hover:border-primary/40 hover:bg-background/70">
            <div className="flex items-start justify-between gap-3">
                <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                    <BriefcaseBusiness className="size-5 text-electric" />
                </div>

                <span className="rounded-full border border-emerald-500/30 bg-emerald-500/10 px-2.5 py-1 text-xs font-medium text-emerald-400">
                    {project.status}
                </span>
            </div>

            <h3 className="mt-5 text-lg font-semibold">
                {project.title}
            </h3>

            <p className="mt-2 flex-1 text-sm leading-6 text-muted-foreground">
                {truncateText(
                    project.description,
                    180,
                )}
            </p>

            <div className="mt-5 grid grid-cols-2 gap-3">
                <ProjectMeta
                    icon={CircleDollarSign}
                    label="Budget"
                    value={formatAmount(
                        project.budget,
                    )}
                />

                <ProjectMeta
                    icon={CircleDollarSign}
                    label="Available"
                    value={formatAmount(
                        remainingBudget,
                    )}
                />

                <ProjectMeta
                    icon={Clock3}
                    label="Milestones"
                    value={String(
                        project.milestoneCount,
                    )}
                />

                <ProjectMeta
                    icon={CalendarDays}
                    label="Deadline"
                    value={formatDate(
                        project.deadline,
                    )}
                />
            </div>

            <Button
                asChild
                className="mt-5 w-full"
            >
                <Link
                    href={`/dashboard/freelancer/marketplace/${project.id}`}
                >
                    View project
                </Link>
            </Button>
        </article>
    );
}

type ProjectMetaProps = {
    icon: typeof Clock3;
    label: string;
    value: string;
};

function ProjectMeta({
    icon: Icon,
    label,
    value,
}: ProjectMetaProps) {
    return (
        <div className="rounded-lg border bg-card/50 p-3">
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

function MarketplaceError({
    message,
    onRetry,
}: {
    message: string;
    onRetry: () => void;
}) {
    return (
        <section className="mt-6 rounded-2xl border border-destructive/30 bg-destructive/5 p-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex gap-3">
                    <TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />

                    <div>
                        <h2 className="font-semibold">
                            Could not load marketplace
                        </h2>

                        <p className="mt-1 text-sm text-muted-foreground">
                            {message}
                        </p>
                    </div>
                </div>

                <Button
                    type="button"
                    variant="outline"
                    onClick={onRetry}
                >
                    <RefreshCw className="size-4" />
                    Try again
                </Button>
            </div>
        </section>
    );
}

function MarketplaceEmpty() {
    return (
        <section className="mt-6 rounded-2xl border border-dashed bg-card/40 p-10 text-center">
            <BriefcaseBusiness className="mx-auto size-10 text-muted-foreground" />

            <h2 className="mt-4 text-lg font-semibold">
                No projects found
            </h2>

            <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">
                Try changing your search,
                budget range or deadline
                filters.
            </p>
        </section>
    );
}

function MarketplaceResultsSkeleton() {
    return (
        <section className="mt-6 animate-pulse rounded-2xl border bg-card/50 p-6">
            <div className="h-7 w-40 rounded bg-muted" />

            <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                {Array.from({
                    length: 6,
                }).map((_, index) => (
                    <div
                        key={index}
                        className="h-80 rounded-xl border bg-muted/40"
                    />
                ))}
            </div>
        </section>
    );
}

export function MarketplacePageSkeleton() {
    return (
        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
            <div className="h-64 animate-pulse rounded-2xl border bg-muted/30" />

            <div className="mt-6 h-64 animate-pulse rounded-2xl border bg-muted/30" />

            <MarketplaceResultsSkeleton />
        </main>
    );
}

function parsePage(
    value: string | null,
) {
    const parsed = Number(value);

    if (
        !Number.isInteger(parsed) ||
        parsed < 1
    ) {
        return 1;
    }

    return Math.min(parsed, 500);
}

function parseBudgetFromUrl(
    value: string | null,
) {
    if (!value) {
        return undefined;
    }

    const parsed = Number(value);

    if (
        !Number.isFinite(parsed) ||
        parsed < 0
    ) {
        return undefined;
    }

    return parsed;
}

function parseBudgetInput(
    value: string,
): number | undefined | null {
    const normalized =
        value
            .trim()
            .replace(",", ".");

    if (!normalized) {
        return undefined;
    }

    if (
        !/^(?:0|[1-9]\d*)(?:\.\d{1,2})?$/.test(
            normalized,
        )
    ) {
        return null;
    }

    const parsed = Number(normalized);

    if (
        !Number.isFinite(parsed) ||
        parsed < 0
    ) {
        return null;
    }

    return parsed;
}

function parseSortOption(
    value: string | null,
): ProjectMarketplaceSortOption {
    const option =
        sortOptions.find(
            (item) =>
                item.value === value,
        );

    return option?.value ?? "Newest";
}

function toEndOfDayIso(
    value: string,
) {
    const parts = value
        .split("-")
        .map(Number);

    if (parts.length !== 3) {
        return undefined;
    }

    const [year, month, day] =
        parts;

    const date = new Date(
        year,
        month - 1,
        day,
        23,
        59,
        59,
        999,
    );

    if (
        Number.isNaN(date.getTime())
    ) {
        return undefined;
    }

    return date.toISOString();
}

function createUrl(
    pathname: string,
    params: URLSearchParams,
) {
    const query =
        params.toString();

    return query
        ? `${pathname}?${query}`
        : pathname;
}

function truncateText(
    value: string,
    maximumLength: number,
) {
    if (
        value.length <= maximumLength
    ) {
        return value;
    }

    return `${value.slice(
        0,
        maximumLength,
    )}...`;
}

function formatAmount(
    value: number,
) {
    return new Intl.NumberFormat(
        "en-US",
        {
            minimumFractionDigits: 0,
            maximumFractionDigits: 2,
        },
    ).format(value);
}

function formatDate(
    value: string,
) {
    const date = new Date(value);

    if (
        Number.isNaN(date.getTime())
    ) {
        return "Unknown";
    }

    return new Intl.DateTimeFormat(
        "en-US",
        {
            dateStyle: "medium",
        },
    ).format(date);
}