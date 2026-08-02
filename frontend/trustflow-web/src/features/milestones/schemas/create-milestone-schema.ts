import { z } from "zod";

const MAX_SAFE_AMOUNT = Number.MAX_SAFE_INTEGER / 100;

const MAX_SEQUENCE_NUMBER = 2_147_483_647;

type CreateMilestoneSchemaOptions = {
  projectDeadline: string;
  remainingBudget: number;
  existingSequenceNumbers: number[];
};

export function createMilestoneSchema({
  projectDeadline,
  remainingBudget,
  existingSequenceNumbers,
}: CreateMilestoneSchemaOptions) {
  const normalizedRemainingBudget =
    Number.isFinite(remainingBudget) && remainingBudget > 0
      ? remainingBudget
      : 0;

  const projectDeadlineDate = new Date(projectDeadline);

  const amountSchema = z
    .string()
    .trim()
    .min(1, "Amount is required.")
    .transform((value) => value.replace(",", "."))
    .refine(
      (value) => /^(?:0|[1-9]\d{0,15})(?:\.\d{1,2})?$/.test(value),
      "Amount must be a valid number with at most 2 decimal places.",
    )
    .refine((value) => Number(value) >= 0.01, "Amount must be at least 0.01.")
    .refine(
      (value) => Number(value) <= MAX_SAFE_AMOUNT,
      "Amount is too large to process safely.",
    )
    .refine(
      (value) => Number(value) <= normalizedRemainingBudget,
      `Amount cannot exceed the remaining budget of ${formatAmount(
        normalizedRemainingBudget,
      )}.`,
    )
    .transform((value) => Number(value));

  const sequenceNumberSchema = z
    .string()
    .trim()
    .min(1, "Sequence number is required.")
    .regex(/^[1-9]\d*$/, "Sequence number must be a positive whole number.")
    .transform((value) => Number(value))
    .refine(Number.isSafeInteger, "Sequence number is too large.")
    .refine(
      (value) => value <= MAX_SEQUENCE_NUMBER,
      `Sequence number cannot exceed ${MAX_SEQUENCE_NUMBER}.`,
    )
    .refine(
      (value) => !existingSequenceNumbers.includes(value),
      "This sequence number already exists.",
    );

  const deadlineSchema = z
    .string()
    .min(1, "Deadline is required.")
    .refine(isValidDateInput, "Enter a valid deadline.")
    .refine(isFutureDateInput, "Deadline must be in the future.")
    .refine((value) => {
      if (
        !isValidDateInput(value) ||
        Number.isNaN(projectDeadlineDate.getTime())
      ) {
        return false;
      }

      const milestoneDeadline = createLocalEndOfDay(value);

      return milestoneDeadline.getTime() <= projectDeadlineDate.getTime();
    }, "Milestone deadline cannot be after the project deadline.")
    .transform((value) => createLocalEndOfDay(value).toISOString());

  return z.object({
    title: z
      .string()
      .trim()
      .min(1, "Milestone title is required.")
      .max(200, "Milestone title cannot exceed 200 characters."),

    description: z
      .string()
      .trim()
      .min(1, "Milestone description is required.")
      .max(5000, "Milestone description cannot exceed 5000 characters."),

    amount: amountSchema,

    sequenceNumber: sequenceNumberSchema,

    deadline: deadlineSchema,
  });
}

export type CreateMilestoneSchema = ReturnType<typeof createMilestoneSchema>;

export type CreateMilestoneFormInput = z.input<CreateMilestoneSchema>;

export type CreateMilestoneFormOutput = z.output<CreateMilestoneSchema>;

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

  return createLocalEndOfDay(value).getTime() > Date.now();
}

function createLocalEndOfDay(value: string) {
  const { year, month, day } = getDateParts(value);

  return new Date(year, month - 1, day, 23, 59, 59, 999);
}

function formatAmount(value: number) {
  return new Intl.NumberFormat("en-US", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value);
}
