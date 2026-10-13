import type { MetadataRoute } from "next";

import { site } from "@/config/site";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      // The signed-in app has nothing public to read.
      disallow: ["/dashboard"],
    },
    sitemap: `${site.url}/sitemap.xml`,
  };
}
