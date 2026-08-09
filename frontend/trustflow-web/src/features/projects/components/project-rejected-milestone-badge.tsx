import {
    CircleAlert,
} from "lucide-react";

type ProjectRejectedMilestoneBadgeProps = {
    count: number;
};

export function ProjectRejectedMilestoneBadge({
    count,
}: ProjectRejectedMilestoneBadgeProps) {
    if (count <= 0) {
        return null;
    }

    const milestoneLabel =
        count === 1
            ? "milestone"
            : "milestones";

    return (
        <span
            role="status"
            aria-label={`${count} rejected ${milestoneLabel}`}
            className="inline-flex items-center gap-2 rounded-full border border-red-500/30 bg-red-500/10 px-3 py-1.5 text-xs font-semibold text-red-300"
        >
            <CircleAlert className="size-4" />

            {count} rejected{" "}
            {milestoneLabel}
        </span>
    );
}