// Where the site lives and how it describes itself to search engines and to
// the apps that show link previews. The address is read at build time, so set
// NEXT_PUBLIC_SITE_URL to the real public address before a production build.
// Without it every canonical link, sitemap entry and preview image would point
// at the local development address.

function readSiteUrl() {
  const configured =
    process.env.NEXT_PUBLIC_SITE_URL ||
    (process.env.VERCEL_PROJECT_PRODUCTION_URL
      ? `https://${process.env.VERCEL_PROJECT_PRODUCTION_URL}`
      : "http://localhost:3000");

  return configured.replace(/\/+$/, "");
}

export const site = {
  name: "TrustFlow AI",
  url: readSiteUrl(),
  title: "Milestone Escrow for Freelance Work",
  description:
    "Hire freelancers with confidence. TrustFlow locks your budget in a smart contract escrow and releases it one milestone at a time, with AI help and a human reviewer.",
  locale: "en_US",
} as const;
