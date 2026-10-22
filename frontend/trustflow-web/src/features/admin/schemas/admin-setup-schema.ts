import { z } from "zod";
import { registerSchema } from "@/features/auth/schemas/register-schema";

export const adminSetupSchema = z.object({
  fullName: registerSchema.shape.fullName,
  email: registerSchema.shape.email,
  password: registerSchema.shape.password,
  confirmPassword: registerSchema.shape.confirmPassword,
}).refine((values) => values.password === values.confirmPassword, {
  message: "Passwords do not match.", path: ["confirmPassword"],
});
export type AdminSetupValues = z.infer<typeof adminSetupSchema>;
