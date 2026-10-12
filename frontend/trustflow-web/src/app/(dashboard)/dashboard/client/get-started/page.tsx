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
            "Choose Individual or Company and add a short introduction. Add a profile photo, adjust the round preview and click Save photo. You can change or remove it any time.",
        icon: UserRoundCheck,
        action: {
            label: "Open my profile",
            href: "/dashboard/client/profile",
        },
    },
    {
        title: "Create a project",
        description:
            "Write a clear title and a short description. Set the full budget and final deadline. Keep the work easy to understand. Not sure what to write or what it should cost? Use Draft with AI: describe your idea and it suggests a title, a description, a price and milestones. You can change everything before you create the project.",
        icon: FilePlus2,
        action: {
            label: "Create a project",
            href: "/dashboard/client/projects/new",
        },
    },
    {
        title: "Add milestones",
        description:
            "Split the work into small steps. Give each step an amount and a deadline. All milestone amounts must add up to the project budget. Until they match it exactly, freelancers cannot see your project and cannot send a proposal.",
        icon: ListChecks,
        action: {
            label: "Open my projects",
            href: "/dashboard/client/projects",
        },
    },
    {
        title: "Choose a freelancer",
        description:
            "You already set the price, so freelancers compete on skill and delivery time, not on cost. Each proposal says which milestones that freelancer will deliver. You can hire one person for everything, or a different person for each milestone. Read each proposal and open the freelancer profile before you accept.",
        icon: BadgeCheck,
        action: {
            label: "Review my projects",
            href: "/dashboard/client/projects",
        },
    },
    {
        title: "Fund the secure payment",
        description:
            "Connect and verify your wallet. Every freelancer on the project gets their own escrow, holding only their own milestones, so do this once for each of them. Create and deploy the escrow. Switch to the shown test network, get test ETH and test tokens, then use Approve and fund escrow.",
        icon: CircleDollarSign,
        action: {
            label: "Open assigned project",
            href: "/dashboard/client/projects",
        },
    },
    {
        title: "Review work and release payment",
        description:
            "Open each submitted milestone. Read the note, download its files and check its links. You can use Check with AI to see if the work matches what you asked for. Then ask for changes with a clear reason, or release payment and approve the work. If you and the freelancer cannot agree, you can open a dispute. If the work never arrives or is not good enough, use Ask for refund.",
        icon: ClipboardCheck,
        action: {
            label: "Go to my projects",
            href: "/dashboard/client/projects",
        },
    },
];

const reminders = [
    "Use the same wallet that you verified on your account.",
    "Disconnect in the top bar only signs the wallet out of this site. Your account keeps the address you verified.",
    "A new project stays hidden until its milestone amounts add up to the budget.",
    "Your project stays on the marketplace while any milestone is still free, so other freelancers can apply for the rest.",
    "Changing the budget after a freelancer applies blocks their proposal until they agree again.",
    "Fund each freelancer’s escrow before that freelancer starts work.",
    "You can cancel and get a refund only before work starts. Cancelling drops that one freelancer and puts their stages back on the marketplace. Your other freelancers keep working.",
    "Release payment only after you check the submitted work.",
    "AI help is advice. It can be wrong. You decide, and you release payment yourself.",
    "A dispute asks an AI to give an opinion on who is right. The opinion does not move any money by itself. While a dispute is open you cannot ask for changes on that milestone.",
    "Ask for refund starts a 14 day clock on the escrow. During it you can still release payment or take the request back. After 14 days a human reviewer can pay the freelancer, pay you back, or split the milestone. They can send the money only to you or the freelancer.",
    "If the freelancer leaves the project, the money not yet paid comes back to you and their unpaid milestones open for hire again.",
    "If the reviewer never answers for 90 days, either of you can split the milestone 50/50 and end the escrow.",
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
