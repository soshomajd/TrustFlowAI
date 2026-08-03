"use client";

import { MilestoneForm } from
    "@/features/milestones/components/milestone-form";
import { useUpdateMilestone } from
    "@/features/milestones/hooks/use-update-milestone";
import type { ProjectMilestone } from
    "@/features/milestones/types/milestone";
import type {
    CreateMilestoneFormOutput,
} from
    "@/features/milestones/schemas/create-milestone-schema";

type EditMilestoneFormProps = {
    projectId: string;
    projectDeadline: string;
    remainingBudget: number;

    existingSequenceNumbers: number[];
    milestone: ProjectMilestone;

    onUpdated: () => void;
    onCancel: () => void;
};

export function EditMilestoneForm({
    projectId,
    projectDeadline,
    remainingBudget,
    existingSequenceNumbers,
    milestone,
    onUpdated,
    onCancel,
}: EditMilestoneFormProps) {
    const updateMutation =
        useUpdateMilestone(projectId);

    const availableBudget =
        roundCurrency(
            remainingBudget +
            milestone.amount,
        );

    const otherSequenceNumbers =
        existingSequenceNumbers.filter(
            (sequenceNumber) =>
                sequenceNumber !==
                milestone.sequenceNumber,
        );

    function handleSubmit(
        values:
            CreateMilestoneFormOutput,
    ) {
        updateMutation.mutate(
            {
                milestoneId:
                    milestone.id,

                request: values,
            },
            {
                onSuccess: onUpdated,
            },
        );
    }

    return (
        <MilestoneForm
            projectDeadline={
                projectDeadline
            }
            availableBudget={
                availableBudget
            }
            existingSequenceNumbers={
                otherSequenceNumbers
            }
            defaultValues={{
                title:
                    milestone.title,

                description:
                    milestone.description,

                amount:
                    String(
                        milestone.amount,
                    ),

                sequenceNumber:
                    String(
                        milestone.sequenceNumber,
                    ),

                deadline:
                    toDateInputValue(
                        milestone.deadline,
                    ),
            }}
            submitLabel="Save changes"
            submittingLabel="Saving changes"
            isSubmitting={
                updateMutation.isPending
            }
            onSubmit={handleSubmit}
            onCancel={onCancel}
        />
    );
}

function roundCurrency(
    value: number,
) {
    return (
        Math.round(
            value * 100,
        ) / 100
    );
}

function toDateInputValue(
    value: string,
) {
    const date = new Date(value);

    if (
        Number.isNaN(date.getTime())
    ) {
        return "";
    }

    const timezoneOffset =
        date.getTimezoneOffset() *
        60_000;

    return new Date(
        date.getTime() -
        timezoneOffset,
    )
        .toISOString()
        .slice(0, 10);
}