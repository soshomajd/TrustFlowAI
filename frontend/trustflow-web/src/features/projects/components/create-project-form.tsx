"use client";

import { ListChecks, X } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { ProjectDraftDialog } from "@/features/ai/components/project-draft-dialog";
import { useAiStatus } from "@/features/ai/hooks/use-ai-status";
import { buildProjectPlan, type ProjectPlan } from "@/features/ai/lib/draft-plan";
import { ProjectForm } from "@/features/projects/components/project-form";
import { useCreateProject } from "@/features/projects/hooks/use-create-project";

const emptyValues = {
    title: "",
    description: "",
    budget: "",
    deadline: "",
};

export function CreateProjectForm() {
    const createProjectMutation = useCreateProject();
    const aiStatus = useAiStatus();

    // A drafted plan fills the form once and then lives next to it. The
    // key restarts the form with the drafted values; typing afterwards is
    // the client's own.
    const [plan, setPlan] = useState<ProjectPlan | null>(null);
    const [formKey, setFormKey] = useState(0);

    return (
        <div className="space-y-5">
            {aiStatus.data?.enabled && (
                <div className="flex flex-col gap-3 rounded-2xl border border-primary/20 bg-primary/5 p-4 sm:flex-row sm:items-center sm:justify-between">
                    <p className="text-sm leading-6 text-muted-foreground">
                        Not sure how to start? Describe your idea and let AI
                        suggest what to write, a fair price and how to split
                        the work into milestones.
                    </p>

                    <ProjectDraftDialog
                        disabled={createProjectMutation.isPending}
                        onApply={(draft) => {
                            setPlan(buildProjectPlan(draft));
                            setFormKey((key) => key + 1);
                        }}
                    />
                </div>
            )}

            {plan && (
                <section className="rounded-2xl border bg-card/70 p-5">
                    <div className="flex items-start justify-between gap-3">
                        <div className="flex items-center gap-2">
                            <ListChecks className="size-4 text-electric" />

                            <h2 className="text-sm font-semibold">
                                Milestone plan from AI
                            </h2>
                        </div>

                        <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            disabled={createProjectMutation.isPending}
                            onClick={() => setPlan(null)}
                        >
                            <X className="size-3.5" />
                            Remove plan
                        </Button>
                    </div>

                    <p className="mt-2 text-xs leading-5 text-muted-foreground">
                        These milestones are created right after the project.
                        The budget must stay equal to their total, which is{" "}
                        {plan.total.toFixed(2)}. You can still edit or delete
                        any milestone later.
                    </p>

                    <ol className="mt-4 space-y-2">
                        {plan.milestones.map((milestone) => (
                            <li
                                key={milestone.sequenceNumber}
                                className="flex items-start justify-between gap-3 rounded-lg border bg-background/35 px-3 py-2 text-sm"
                            >
                                <span>
                                    {milestone.sequenceNumber}.{" "}
                                    {milestone.title}
                                </span>

                                <span className="shrink-0 text-electric">
                                    {milestone.amount.toFixed(2)}
                                </span>
                            </li>
                        ))}
                    </ol>
                </section>
            )}

            <ProjectForm
                key={formKey}
                defaultValues={plan?.formValues ?? emptyValues}
                isPending={createProjectMutation.isPending}
                submitLabel="Create project"
                pendingLabel="Creating project..."
                cancelHref="/dashboard/client/projects"
                onSubmit={(values) => {
                    if (plan) {
                        if (
                            Math.round(values.budget * 100) !==
                            Math.round(plan.total * 100)
                        ) {
                            toast.error(
                                `The budget must equal the plan total (${plan.total.toFixed(2)}). ` +
                                    "Change the budget or remove the plan.",
                            );

                            return;
                        }

                        const lastDeadline = plan.milestones.reduce(
                            (latest, milestone) =>
                                milestone.deadline > latest
                                    ? milestone.deadline
                                    : latest,
                            "",
                        );

                        if (values.deadline < lastDeadline) {
                            toast.error(
                                "The project deadline is before the last milestone in the plan. Move the deadline later or remove the plan.",
                            );

                            return;
                        }
                    }

                    createProjectMutation.mutate({
                        project: values,
                        milestones: plan?.milestones,
                    });
                }}
            />
        </div>
    );
}
