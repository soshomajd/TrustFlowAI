import { z } from "zod";

const MAX_SAFE_BUDGET = Number.MAX_SAFE_INTEGER / 100;

const budgetSchema = z
  .string()
  .trim()
  .min(1, "Budget is required.")
  .transform((value) => value.replace(",", "."))
  .refine(
    (value) => /^(?:0|[1-9]\d{0,15})(?:\.\d{1,2})?$/.test(value),
    "Budget must be a valid number with at most 2 decimal places.",
  )
  .refine((value) => Number(value) >= 0.01, "Budget must be at least 0.01.")
  .refine(
    (value) => Number(value) <= MAX_SAFE_BUDGET,
    "Budget is too large to process safely.",
  )
  .transform((value) => Number(value));

const deadlineSchema = z
  .string()
  .min(1, "Deadline is required.")
  .refine(isValidDateInput, "Enter a valid deadline.")
  .refine(isFutureDateInput, "Deadline must be in the future.")
  .transform(convertDateInputToUtc);

export const createProjectSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, "Project title is required.")
    .max(200, "Project title cannot exceed 200 characters."),

  description: z
    .string()
    .trim()
    .min(1, "Project description is required.")
    .max(5000, "Project description cannot exceed 5000 characters."),

  budget: budgetSchema,

  deadline: deadlineSchema,
});

export type CreateProjectFormInput = z.input<typeof createProjectSchema>;

export type CreateProjectFormOutput = z.output<typeof createProjectSchema>;

function getDateParts(value: string) {
  const parts = value.split("-").map(Number);

  return {
    year: parts[0],
    month: parts[1],
    day: parts[2],
  };
}

function isValidDateInput(value: string) {
  const { year, month, day } = getDateParts(value);

  if (!year || !month || !day) {
    return false;
  }

  const date = new Date(year, month - 1, day);

  return (
    date.getFullYear() === year &&
    date.getMonth() === month - 1 &&
    date.getDate() === day
  );
}

function isFutureDateInput(value: string) {
  if (!isValidDateInput(value)) {
    return false;
  }

  const deadline = createLocalEndOfDay(value);

  return deadline.getTime() > Date.now();
}

function createLocalEndOfDay(value: string) {
  const { year, month, day } = getDateParts(value);

  return new Date(year, month - 1, day, 23, 59, 59, 999);
}

function convertDateInputToUtc(value: string) {
  return createLocalEndOfDay(value).toISOString();
}
