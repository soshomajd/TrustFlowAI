"use client";
import { moneyText } from "@/lib/money/exact-money";
import { sumMoney } from "@/lib/money/exact-money";

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
    remainingBudget: number | string;

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
        sumMoney([remainingBudget, moneyText(milestone, "amount")]);

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
                    moneyText(milestone, "amount"),

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
