import { z } from "zod";

const emailField = z
  .string()
  .trim()
  .min(1, "Email is required.")
  .email("Enter a valid email address.");

const codeField = z
  .string()
  .trim()
  .regex(/^\d{6}$/, "Enter the 6 digit code from the email.");

// The same rules the register form and the server apply, so a password
// accepted here is not rejected after the code has been entered.
const newPasswordField = z
  .string()
  .min(8, "Password must be at least 8 characters.")
  .regex(/[a-z]/, "Password must contain a lowercase letter.")
  .regex(/\d/, "Password must contain a number.");

export const verifyEmailSchema = z.object({
  email: emailField,
  code: codeField,
});

export type VerifyEmailFormValues = z.infer<typeof verifyEmailSchema>;

export const forgotPasswordSchema = z.object({
  email: emailField,
});

export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z
  .object({
    email: emailField,
    code: codeField,
    newPassword: newPasswordField,
    confirmPassword: z.string().min(1, "Confirm your new password."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

export type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export const resendEmailSchema = emailField;
