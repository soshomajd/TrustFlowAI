import type { Metadata } from "next";
import {
    BriefcaseBusiness,
    CircleDollarSign,
    FileUser,
    Search,
    Send,
    Wallet,
} from "lucide-react";

import {
    GettingStartedGuide,
    type GuideStep,
} from "@/features/guides/components/getting-started-guide";

export const metadata: Metadata = {
    title: "Freelancer getting started guide",
};

const steps: GuideStep[] = [
    {
        title: "Build your profile",
        description:
            "Add a short headline, your main skills and a simple introduction. Use Save changes in any section. Add work samples or one PDF resume if you have them.",
        icon: FileUser,
        action: {
            label: "Open my profile",
            href: "/dashboard/freelancer/profile",
        },
    },
    {
        title: "Connect your wallet",
        description:
            "Use Connect wallet and sign the message to verify that the wallet is yours. You need this wallet later to start paid work.",
        icon: Wallet,
    },
    {
        title: "Find a project",
        description:
            "Open the marketplace. Read the project details, full budget, deadline and milestones. Choose work that matches your skills and time.",
        icon: Search,
        action: {
            label: "Browse marketplace",
            href: "/dashboard/freelancer/marketplace",
        },
    },
    {
        title: "Send a clear proposal",
        description:
            "Enter your price, delivery time and a short message. Explain how you will help. If it is still pending, you can withdraw it from My proposals.",
        icon: Send,
        action: {
            label: "Open my proposals",
            href: "/dashboard/freelancer/proposals",
        },
    },
    {
        title: "Wait for secure funding",
        description:
            "When a client accepts you, the project appears in Assigned projects. Do not start yet. Wait until the escrow status says Funded.",
        icon: CircleDollarSign,
        action: {
            label: "Open assigned projects",
            href: "/dashboard/freelancer/projects",
        },
    },
    {
        title: "Do and submit the work",
        description:
            "When escrow is funded, use Start work. Finish the milestone, then submit a note with files or secure links. If the client asks for changes, read the feedback, fix the work and submit a new version.",
        icon: BriefcaseBusiness,
        action: {
            label: "Go to my projects",
            href: "/dashboard/freelancer/projects",
        },
    },
];

const reminders = [
    "Use the same wallet that you verified on your account.",
    "Do not start work until the escrow status says Funded.",
    "Finish milestones in order, one at a time.",
    "Keep your proposal and submitted work clear and short.",
];

export default function FreelancerGettingStartedPage() {
    return (
        <GettingStartedGuide
            role="Freelancer"
            title="How to find work and get paid"
            description="This short guide shows the full path. Build your profile, send a proposal, wait for secure funding and submit your work."
            startAction={{
                label: "Start with my profile",
                href: "/dashboard/freelancer/profile",
            }}
            steps={steps}
            reminders={reminders}
        />
    );
}
