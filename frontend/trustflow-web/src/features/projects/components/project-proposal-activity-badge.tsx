import {
    BellRing,
} from "lucide-react";

type ProjectProposalActivityBadgeProps = {
    count: number;
    label?: "new" | "pending";
};

export function ProjectProposalActivityBadge({
    count,
    label = "pending",
}: ProjectProposalActivityBadgeProps) {
    if (count <= 0) {
        return null;
    }

    const visibleCount =
        count > 99 ? "99+" : String(count);

    const proposalLabel =
        count === 1
            ? "proposal"
            : "proposals";

    return (
        <div
            role="status"
            aria-label={`${count} ${label} ${proposalLabel}`}
            className="inline-flex items-center gap-2 rounded-full border border-amber-500/30 bg-amber-500/10 px-3 py-1.5 text-xs font-semibold text-amber-300 shadow-sm"
        >
            <span className="relative flex">
                <BellRing className="size-4" />

                <span className="absolute -right-1 -top-1 size-2 rounded-full bg-amber-400 ring-2 ring-background" />
            </span>

            <span>
                {visibleCount} {label}{" "}
                {proposalLabel}
            </span>
        </div>
    );
}