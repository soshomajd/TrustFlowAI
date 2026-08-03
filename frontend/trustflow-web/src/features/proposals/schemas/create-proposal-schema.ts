import { z } from "zod";

const MAX_SAFE_AMOUNT = Number.MAX_SAFE_INTEGER / 100;

const MAX_ESTIMATED_DAYS = 2_147_483_647;

export const createProposalSchema = z.object({
  coverLetter: z
    .string()
    .trim()
    .min(1, "Cover letter is required.")
    .max(5000, "Cover letter cannot exceed 5000 characters."),

  bidAmount: z
    .string()
    .trim()
    .min(1, "Bid amount is required.")
    .transform((value) => value.replace(",", "."))
    .refine(
      (value) => /^(?:0|[1-9]\d*)(?:\.\d{1,2})?$/.test(value),
      "Bid amount must be a valid number with at most 2 decimal places.",
    )
    .refine(
      (value) => Number(value) >= 0.01,
      "Bid amount must be at least 0.01.",
    )
    .refine(
      (value) => Number(value) <= MAX_SAFE_AMOUNT,
      "Bid amount is too large to process safely.",
    )
    .transform((value) => Number(value)),

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
