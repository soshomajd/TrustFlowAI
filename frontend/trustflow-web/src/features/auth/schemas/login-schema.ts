import { z } from "zod";

export const loginSchema = z.object({
  email: z.email("Enter a valid email address.").min(1, "Email is required").trim(),
  password: z.string().min(1, "Password is required."),
});

export type LoginFormValues = z.infer<typeof loginSchema>;
