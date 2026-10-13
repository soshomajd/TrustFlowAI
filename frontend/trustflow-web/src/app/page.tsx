import type { Metadata } from "next";

import { LandingPage } from "@/features/landing/components/landing-page";

export const metadata: Metadata = {
    title: {
        absolute: "TrustFlow AI | Hire with confidence, pay when the work is done",
    },
    description:
        "Milestone escrow for freelance work. Your budget is held by a smart contract and released one milestone at a time, with AI help and a human reviewer if you disagree.",
};

export default function HomePage() {
    return <LandingPage />;
}
