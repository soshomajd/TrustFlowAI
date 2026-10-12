import type { ProjectDraft } from "@/features/ai/types/ai";
import type { CreateMilestoneRequest } from "@/features/milestones/types/milestone";

// Days after the last milestone that the project itself stays open, so the
// project deadline is never earlier than a milestone deadline.
const PROJECT_BUFFER_DAYS = 7;

export function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");

  return `${year}-${month}-${day}`;
}

function daysFromToday(days: number) {
  const date = new Date();

  date.setDate(date.getDate() + days);

  return date;
}

// The same rule the project form uses: a chosen day ends at 23:59 local time.
function endOfLocalDayIso(date: Date) {
  return new Date(
    date.getFullYear(),
    date.getMonth(),
    date.getDate(),
    23,
    59,
    59,
    999,
  ).toISOString();
}

export type ProjectPlan = {
  // Ready for the project form's own fields.
  formValues: {
    title: string;
    description: string;
    budget: string;
    deadline: string;
  };

  // Created one by one after the project exists.
  milestones: CreateMilestoneRequest[];

  total: number;
};

export function buildProjectPlan(draft: ProjectDraft): ProjectPlan {
  const lastDue = Math.max(
    ...draft.milestones.map((milestone) => milestone.dueInDays),
  );

  const milestones = draft.milestones.map((milestone, index) => ({
    title: milestone.title,
    description: milestone.description,
    amount: milestone.amount,
    sequenceNumber: index + 1,
    deadline: endOfLocalDayIso(daysFromToday(milestone.dueInDays)),
  }));

  return {
    formValues: {
      title: draft.title,
      description: draft.description,
      budget: draft.budget.toFixed(2),
      deadline: toDateInputValue(
        daysFromToday(lastDue + PROJECT_BUFFER_DAYS),
      ),
    },

    milestones,

    // Cents, so floating point cannot make a matching total look different.
    total:
      milestones.reduce(
        (sum, milestone) => sum + Math.round(milestone.amount * 100),
        0,
      ) / 100,
  };
}
