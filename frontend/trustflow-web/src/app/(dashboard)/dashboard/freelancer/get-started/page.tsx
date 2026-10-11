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
            "Add a short headline, your main skills and a simple introduction. Use Save changes in any section. Add work samples or one PDF resume if you have them. Add a photo, adjust the round preview and click Save photo. You can change or remove it any time.",
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
            "Open the marketplace. Search by any word, such as React or design: it looks in the project title, the details and every milestone. Read the full budget, deadline and milestones, then choose work that matches your skills and time.",
        icon: Search,
        action: {
            label: "Browse marketplace",
            href: "/dashboard/freelancer/marketplace",
        },
    },
    {
        title: "Send a clear proposal",
        description:
            "Pick the milestones you want to deliver. You can take one, a few, or all of them; the ones another freelancer already has are marked as taken. Your price is the total of the milestones you pick, because the client sets the price. Add your delivery time and a short message. If it is still pending, you can withdraw it from My proposals.",
        icon: Send,
        action: {
            label: "Open my proposals",
            href: "/dashboard/freelancer/proposals",
        },
    },
    {
        title: "Wait for secure funding",
        description:
            "When a client accepts you, the project appears in Assigned projects. You get your own escrow for your own milestones, even when other freelancers work on the same project. Do not start yet. Wait until your escrow status says Funded. Then use your verified wallet to start work on-chain.",
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
    "Disconnect in the top bar only signs the wallet out of this site. Your account keeps the address you verified.",
    "Do not start work until the escrow status says Funded.",
    "The client can cancel the escrow until you start work on-chain.",
    "Finish your own milestones in order, one at a time. Another freelancer being slow does not block you.",
    "You cannot offer a lower price. The milestones you pick set the price you are paid.",
    "A project can stay on the marketplace after someone is hired. The milestones nobody has taken are still open to you.",
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
