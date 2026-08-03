"use client";

import { MilestoneForm } from
    "@/features/milestones/components/milestone-form";
import { useCreateMilestone } from
    "@/features/milestones/hooks/use-create-milestone";
import type {
    CreateMilestoneFormOutput,
} from
    "@/features/milestones/schemas/create-milestone-schema";

type CreateMilestoneFormProps = {
    projectId: string;
    projectDeadline: string;
    remainingBudget: number;
    existingSequenceNumbers: number[];

    onCreated: () => void;
    onCancel: () => void;
};

export function CreateMilestoneForm({
    projectId,
    projectDeadline,
    remainingBudget,
    existingSequenceNumbers,
    onCreated,
    onCancel,
}: CreateMilestoneFormProps) {
    const createMutation =
        useCreateMilestone(projectId);

    const suggestedSequenceNumber =
        getNextSequenceNumber(
            existingSequenceNumbers,
        );

    function handleSubmit(
        values:
            CreateMilestoneFormOutput,
    ) {
        createMutation.mutate(
            values,
            {
                onSuccess: onCreated,
            },
        );
    }

    return (
        <MilestoneForm
            projectDeadline={
                projectDeadline
            }
            availableBudget={
                remainingBudget
            }
            existingSequenceNumbers={
                existingSequenceNumbers
            }
            defaultValues={{
                title: "",
                description: "",
                amount: "",

                sequenceNumber:
                    String(
                        suggestedSequenceNumber,
                    ),

                deadline: "",
            }}
            submitLabel="Create milestone"
            submittingLabel="Creating milestone"
            isSubmitting={
                createMutation.isPending
            }
            onSubmit={handleSubmit}
            onCancel={onCancel}
        />
    );
}

function getNextSequenceNumber(
    sequenceNumbers: number[],
) {
    if (
        sequenceNumbers.length === 0
    ) {
        return 1;
    }

    return (
        Math.max(
            ...sequenceNumbers,
        ) + 1
    );
}