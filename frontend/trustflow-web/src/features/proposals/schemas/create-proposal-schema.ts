import { z } from "zod";

const MAX_ESTIMATED_DAYS = 2_147_483_647;

export const createProposalSchema = z.object({
  milestoneIds: z
    .array(z.string())
    .min(1, "Choose at least one milestone you want to deliver."),

  coverLetter: z
    .string()
    .trim()
    .min(1, "Cover letter is required.")
    .max(5000, "Cover letter cannot exceed 5000 characters."),

  estimatedDays: z
    .string()
    .trim()
    .min(1, "Estimated days is required.")
    .regex(/^[1-9]\d*$/, "Estimated days must be a positive whole number.")
    .transform((value) => Number(value))
    .refine(Number.isSafeInteger, "Estimated days is too large.")
    .refine(
      (value) => value <= MAX_ESTIMATED_DAYS,
      `Estimated days cannot exceed ${MAX_ESTIMATED_DAYS}.`,
    ),
});

export type CreateProposalFormInput = z.input<typeof createProposalSchema>;

export type CreateProposalFormOutput = z.output<typeof createProposalSchema>;
