import { EyeOff } from "lucide-react";

import type { ProjectStatus } from "@/features/projects/types/project";

type ProjectHiddenBadgeProps = {
    status: ProjectStatus;
    budget: number;
    allocatedAmount: number;
};

// The marketplace only lists an open project once its milestone amounts add
// up to the budget, so without this the client sees no proposals and no
// reason why.
export function ProjectHiddenBadge({
    status,
    budget,
    allocatedAmount,
}: ProjectHiddenBadgeProps) {
    if (status !== "Open" || allocatedAmount === budget) {
        return null;
    }

    const remaining = budget - allocatedAmount;

    const remainingLabel = new Intl.NumberFormat("en-US", {
        style: "currency",
        currency: "USD",
        maximumFractionDigits: 0,
    }).format(Math.abs(remaining));

    return (
        <div
            role="status"
            aria-label={
                remaining > 0
                    ? `Hidden from freelancers, ${remainingLabel} of the budget is not assigned to milestones`
                    : `Hidden from freelancers, milestones exceed the budget by ${remainingLabel}`
            }
            className="inline-flex items-center gap-2 rounded-full border border-destructive/40 bg-destructive/10 px-3 py-1.5 text-xs font-semibold text-destructive shadow-sm"
        >
            <span className="relative flex">
                <EyeOff className="size-4" />

                <span className="absolute -top-1 -right-1 size-2 rounded-full bg-destructive ring-2 ring-background" />
            </span>

            <span>
                Hidden &middot;{" "}
                {remaining > 0
                    ? `${remainingLabel} unassigned`
                    : `${remainingLabel} over budget`}
            </span>
        </div>
    );
}
