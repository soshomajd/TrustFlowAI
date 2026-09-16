import { z } from "zod";

export function splitEntries(value: string) {
  const entries = value.split(/[,،\n]/).map((entry) => entry.trim()).filter(Boolean);
  return entries.filter((entry, index) => entries.findIndex((other) => other.toLowerCase() === entry.toLowerCase()) === index);
}

function entryList(max: number) {
  return z.string().max(1000).refine((value) => {
    const entries = splitEntries(value);
    return entries.length <= max && entries.every((entry) => entry.length <= 40);
  }, `Add up to ${max} entries, with at most 40 characters each.`);
}

export function isSafePortfolioUrl(value: string) {
  try {
    const url = new URL(value);
    return ["https:", "http:"].includes(url.protocol) && !url.username && !url.password;
  } catch { return false; }
}

export const freelancerProfileSchema = z.object({
  headline: z.string().trim().max(100),
  about: z.string().trim().max(1200),
  location: z.string().trim().max(100),
  yearsOfExperience: z.string().refine((value) => value === "" || (/^\d{1,2}$/.test(value) && Number(value) <= 60), "Enter a whole number from 0 to 60, or leave blank."),
  availability: z.enum(["NotSpecified", "Available", "Limited", "Unavailable"]),
  skills: entryList(10),
  languages: entryList(5),
  portfolioItems: z.array(z.object({
    title: z.string().trim().min(1, "Give this work sample a title.").max(100),
    description: z.string().trim().max(500),
    url: z.string().trim().max(500).refine((value) => !value || isSafePortfolioUrl(value), "Use a full http:// or https:// link without embedded credentials."),
  })).max(3),
});

export type FreelancerProfileFormValues = z.infer<typeof freelancerProfileSchema>;
