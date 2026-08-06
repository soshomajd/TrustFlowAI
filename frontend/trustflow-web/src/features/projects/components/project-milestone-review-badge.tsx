import {
    ClipboardCheck,
} from "lucide-react";

type ProjectMilestoneReviewBadgeProps = {
    count: number;
};

export function ProjectMilestoneReviewBadge({
    count,
}: ProjectMilestoneReviewBadgeProps) {
    if (count <= 0) {
        return null;
    }

    const visibleCount =
        count > 99
            ? "99+"
            : String(count);

    const milestoneLabel =
        count === 1
            ? "milestone"
            : "milestones";

    return (
        <div
            role="status"
            aria-label={`${count} submitted ${milestoneLabel} awaiting review`}
            className="inline-flex items-center gap-2 rounded-full border border-violet-500/30 bg-violet-500/10 px-3 py-1.5 text-xs font-semibold text-violet-300 shadow-sm"
        >
            <span className="relative flex">
                <ClipboardCheck className="size-4" />

                <span className="absolute -right-1 -top-1 size-2 rounded-full bg-violet-400 ring-2 ring-background" />
            </span>

            <span>
                {visibleCount} submitted{" "}
                {milestoneLabel}
            </span>
        </div>
    );
}