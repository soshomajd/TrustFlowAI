import * as z from "zod";

const clientEnvironmentSchema = z.object({
  NEXT_PUBLIC_API_URL: z.url(),
});

const parsedEnvironment = clientEnvironmentSchema.safeParse({
  NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
});

if (!parsedEnvironment.success) {
  console.error(
    "Invalid frontend environment variables:",
    z.flattenError(parsedEnvironment.error).fieldErrors,
  );

  throw new Error("Frontend environment configuration is invalid.");
}

export const env = {
  apiUrl: parsedEnvironment.data.NEXT_PUBLIC_API_URL.replace(/\/+$/, ""),
} as const;
