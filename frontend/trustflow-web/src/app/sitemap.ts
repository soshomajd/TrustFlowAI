import type { MetadataRoute } from "next";

import { site } from "@/config/site";

// Only pages a visitor can open without an account. Project pages and the
// dashboard need a sign-in, and the password and email steps are private.
export default function sitemap(): MetadataRoute.Sitemap {
  return [
    { url: `${site.url}/`, changeFrequency: "monthly", priority: 1 },
    { url: `${site.url}/register`, changeFrequency: "yearly", priority: 0.6 },
    { url: `${site.url}/login`, changeFrequency: "yearly", priority: 0.3 },
  ];
}
