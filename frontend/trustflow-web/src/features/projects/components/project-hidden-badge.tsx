import { moneyUnits, subtractMoney, formatMoneyCurrency } from "@/lib/money/exact-money";
import { EyeOff } from "lucide-react";

import type { ProjectStatus } from "@/features/projects/types/project";

type ProjectHiddenBadgeProps = {
    status: ProjectStatus;
    budget: number | string;
    allocatedAmount: number | string;
};

// The marketplace only lists an open project once its milestone amounts add
// up to the budget, so without this the client sees no proposals and no
// reason why.
export function ProjectHiddenBadge({
    status,
    budget,
    allocatedAmount,
}: ProjectHiddenBadgeProps) {
    if (status !== "Open" || moneyUnits(allocatedAmount) === moneyUnits(budget)) {
        return null;
    }

    const remaining = subtractMoney(budget, allocatedAmount);

    const remainingLabel = formatMoneyCurrency(remaining.replace(/^-/, ""));

    return (
        <div
            role="status"
            aria-label={
                moneyUnits(remaining) > 0n
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
                {moneyUnits(remaining) > 0n
                    ? `${remainingLabel} unassigned`
                    : `${remainingLabel} over budget`}
            </span>
        </div>
    );
}
