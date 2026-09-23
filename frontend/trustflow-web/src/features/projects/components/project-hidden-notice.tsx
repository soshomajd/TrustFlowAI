import { EyeOff } from "lucide-react";

import type { ProjectStatus } from "@/features/projects/types/project";

type ProjectHiddenNoticeProps = {
    status: ProjectStatus;
    budget: number;
    allocatedAmount: number;
    milestoneCount: number;
};

const amountFormatter = new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 2,
});

// Explains the marketplace rule at the moment it bites: an open project is
// only listed once its milestone amounts add up to the budget exactly.
export function ProjectHiddenNotice({
    status,
    budget,
    allocatedAmount,
    milestoneCount,
}: ProjectHiddenNoticeProps) {
    if (status !== "Open" || allocatedAmount === budget) {
        return null;
    }

    const difference = budget - allocatedAmount;

    return (
        <div
            role="status"
            className="mt-6 flex gap-3 rounded-2xl border border-destructive/40 bg-destructive/10 p-5"
        >
            <EyeOff className="mt-0.5 size-5 shrink-0 text-destructive" />

            <div className="min-w-0">
                <p className="font-semibold text-destructive">
                    Freelancers cannot see this project yet
                </p>

                <p className="mt-1 text-sm text-muted-foreground">
                    {milestoneCount === 0
                        ? "Add milestones that add up to the full budget. Until then this project stays out of the marketplace and nobody can send a proposal."
                        : difference > 0
                          ? `${amountFormatter.format(difference)} of the ${amountFormatter.format(budget)} budget is not assigned to a milestone yet. Assign all of it to make this project visible.`
                          : `Your milestones add up to ${amountFormatter.format(Math.abs(difference))} more than the ${amountFormatter.format(budget)} budget. Lower a milestone or raise the budget to make this project visible.`}
                </p>
            </div>
        </div>
    );
}
