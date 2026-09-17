import type { Metadata } from "next";
import {
    BadgeCheck,
    CircleDollarSign,
    ClipboardCheck,
    FilePlus2,
    ListChecks,
    UserRoundCheck,
} from "lucide-react";

import {
    GettingStartedGuide,
    type GuideStep,
} from "@/features/guides/components/getting-started-guide";

export const metadata: Metadata = {
    title: "Client getting started guide",
};

const steps: GuideStep[] = [
    {
        title: "Complete your profile",
        description:
            "Add the basic details that freelancers need to trust you. Choose Individual or Company. You can also send one identity PDF for review.",
        icon: UserRoundCheck,
        action: {
            label: "Open my profile",
            href: "/dashboard/client/profile",
        },
    },
    {
        title: "Create a project",
        description:
            "Write a clear title and a short description. Set the full budget and final deadline. Keep the work easy to understand.",
        icon: FilePlus2,
        action: {
            label: "Create a project",
            href: "/dashboard/client/projects/new",
        },
    },
    {
        title: "Add milestones",
        description:
            "Split the work into small steps. Give each step an amount and a deadline. All milestone amounts must add up to the project budget.",
        icon: ListChecks,
        action: {
            label: "Open my projects",
            href: "/dashboard/client/projects",
        },
    },
    {
        title: "Choose a freelancer",
        description:
            "Read each proposal and open the freelancer profile. Check the price, delivery time, skills and work samples before you accept.",
        icon: BadgeCheck,
        action: {
            label: "Review my projects",
            href: "/dashboard/client/projects",
        },
    },
    {
        title: "Fund the secure payment",
        description:
            "Connect and verify your wallet. Create and deploy the escrow. On the local test network, get test funds, then use Approve and fund escrow.",
        icon: CircleDollarSign,
        action: {
            label: "Open assigned project",
            href: "/dashboard/client/projects",
        },
    },
    {
        title: "Review work and release payment",
        description:
            "Open each submitted milestone. Read the note, download its files and check its links. Ask for changes with a clear reason, or release payment and approve the work.",
        icon: ClipboardCheck,
        action: {
            label: "Go to my projects",
            href: "/dashboard/client/projects",
        },
    },
];

const reminders = [
    "Use the same wallet that you verified on your account.",
    "Check the full project price before you accept a proposal.",
    "Fund the escrow before the freelancer starts work.",
    "Release payment only after you check the submitted work.",
];

export default function ClientGettingStartedPage() {
    return (
        <GettingStartedGuide
            role="Client"
            title="How to hire and pay safely"
            description="This short guide shows the full path. Start with your profile, hire a freelancer, fund the project and approve finished work."
            startAction={{
                label: "Start with my profile",
                href: "/dashboard/client/profile",
            }}
            steps={steps}
            reminders={reminders}
        />
    );
}
