import { z } from "zod";

export const clientProfileSchema = z.object({
  accountType: z.enum(["Individual", "Company"]),
  companyName: z.string().trim().max(120, "Company name must be 120 characters or fewer."),
  overview: z.string().trim().max(1000, "Overview must be 1000 characters or fewer."),
  industry: z.string().trim().max(80, "Industry must be 80 characters or fewer."),
  location: z.string().trim().max(100, "Location must be 100 characters or fewer."),
  website: z.string().trim().max(500).refine((value) => {
    if (!value) return true;
    try { const url = new URL(value); return ["http:", "https:"].includes(url.protocol) && !url.username && !url.password; }
    catch { return false; }
  }, "Enter a complete HTTP(S) website URL."),
  companySize: z.enum(["NotSpecified", "JustMe", "2-10", "11-50", "51-200", "201Plus"]),
}).superRefine((value, context) => {
  if (value.accountType === "Company" && !value.companyName) {
    context.addIssue({ code: "custom", path: ["companyName"], message: "Company name is required." });
  }
});

export type ClientProfileFormValues = z.infer<typeof clientProfileSchema>;
