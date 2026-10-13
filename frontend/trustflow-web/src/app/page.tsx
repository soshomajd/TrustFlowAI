import type { Metadata } from "next";

import { site } from "@/config/site";
import { DeferredSessionCheck } from "@/features/auth/components/deferred-session-check";
import { JsonLd } from "@/features/landing/components/json-ld";
import { LandingPage } from "@/features/landing/components/landing-page";
import { faqs } from "@/features/landing/content";

const title = `${site.title} | ${site.name}`;

export const metadata: Metadata = {
    title: {
        absolute: title,
    },
    description: site.description,
    alternates: {
        canonical: "/",
    },
    openGraph: {
        type: "website",
        url: "/",
        siteName: site.name,
        locale: site.locale,
        title,
        description: site.description,
    },
    twitter: {
        card: "summary_large_image",
        title,
        description: site.description,
    },
};

// Structured data describes only what the page already says: who runs the
// site, what the product is, and the questions answered at the bottom. There
// are no ratings, prices or reviews because the product has none to claim.
const structuredData = {
    "@context": "https://schema.org",
    "@graph": [
        {
            "@type": "Organization",
            "@id": `${site.url}/#organization`,
            name: site.name,
            url: site.url,
            logo: `${site.url}/icon-512.png`,
        },
        {
            "@type": "WebSite",
            "@id": `${site.url}/#website`,
            url: site.url,
            name: site.name,
            description: site.description,
            inLanguage: "en",
            publisher: { "@id": `${site.url}/#organization` },
        },
        {
            "@type": "WebApplication",
            "@id": `${site.url}/#app`,
            name: site.name,
            url: site.url,
            description: site.description,
            applicationCategory: "BusinessApplication",
            operatingSystem: "Any",
            browserRequirements: "Requires JavaScript and a browser wallet",
            publisher: { "@id": `${site.url}/#organization` },
        },
        {
            "@type": "FAQPage",
            "@id": `${site.url}/#faq`,
            mainEntity: faqs.map((item) => ({
                "@type": "Question",
                name: item.question,
                acceptedAnswer: {
                    "@type": "Answer",
                    text: item.answer,
                },
            })),
        },
    ],
};

export default function HomePage() {
    return (
        <>
            <JsonLd data={structuredData} />

            <DeferredSessionCheck />

            <LandingPage />
        </>
    );
}
