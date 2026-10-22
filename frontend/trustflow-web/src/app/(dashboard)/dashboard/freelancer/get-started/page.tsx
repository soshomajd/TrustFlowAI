import type { Metadata } from "next";
import {
    BriefcaseBusiness,
    CircleDollarSign,
    FileUser,
    MessageSquare,
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
            "Add a short headline, your main skills and a simple introduction. Use Save changes in any section. Add work samples or one PDF resume if you have them. Add a photo, adjust the round preview and click Save photo. You can change or remove it any time. Use AI help to review your profile, get wording ideas and draft a resume from your finished work. It only suggests: you choose what to keep and you save it.",
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
            "Pick the milestones you want to deliver. You can take one, a few, or all of them; the ones another freelancer already has are marked as taken. Your price is the total of the milestones you pick, because the client sets the price. Add your delivery time and a short message. If it is still pending, you can withdraw it from My proposals. After rejection or withdrawal, you can apply again with the current milestones and price, unless you already have an escrow on this project. The site checks before you open the form. If you have an escrow, even a cancelled one, Submit proposal is disabled and explains why. Find that contract in Assigned projects.",
        icon: Send,
        action: {
            label: "Open my proposals",
            href: "/dashboard/freelancer/proposals",
        },
    },
    {
        title: "Answer questions in Messages",
        description:
            "Once you send a proposal, you can message the client, and the client can message you. You can send plain text only. Phone numbers, email addresses, links and messenger accounts are blocked, so the conversation and your payment stay protected by the escrow. Put your work samples on your profile and send finished work through the delivery form. Use Load older messages to read earlier conversations. A red number on the bell tells you when you have a new message.",
        icon: MessageSquare,
        action: {
            label: "Open Messages",
            href: "/dashboard/messages",
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
            "When escrow is funded, use Start work and confirm it in your wallet. Start milestone appears after that confirmation. Finish the milestone, then submit a note with files or secure links. If the client asks for changes, read the feedback, fix the work and submit a new version. If you and the client cannot agree about your delivery, you can open a dispute. If the client stays silent after you deliver, use Ask for payment.",
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
    "The client funds the exact total recorded in your escrow contract. Token balances can show more decimal places than the project price because tokens have smaller units.",
    "If your wallet confirms Start work but the page does not update, use Refresh on-chain status. If you retry Start work, the site first checks whether it has already started. Wait for any pending wallet transaction to finish before trying again.",
    "If the client says funding or a refund is confirmed but the page has not updated, use Refresh on-chain status. It does not move money or ask you to sign. Keep waiting until the status is Funded before starting work.",
    "The client can cancel the escrow until you start work on-chain. If they do, you leave the project, your proposal is marked rejected, and your stages go back on the marketplace for others. A notification on the bell tells you what happened, even if you were offline. Open it to view the escrow history, or choose Mark as read to clear it.",
    "Finish your own milestones in order, one at a time. Another freelancer being slow does not block you.",
    "You cannot offer a lower price. The milestones you pick set the price you are paid.",
    "A project can stay on the marketplace after someone is hired. The milestones nobody has taken are still open to you.",
    "Keep your proposal and submitted work clear and short.",
    "A dispute asks an AI to give an opinion on who is right, using the request, your delivered work and what you both write. The opinion does not move any money by itself.",
    "Use a desktop browser with a wallet extension such as MetaMask installed. If no browser wallet is found, open TrustFlow in that browser and try again.",
    "Past escrows stay in Assigned projects after cancellation or resignation. Use View escrow history to check them. Open Transaction history for recorded funding and cancellation transactions. Use Open Messages to read any existing conversations. After you leave a project, its conversation is closed to new messages.",
    "The client cannot delete a project that has an escrow record, so its payment history stays available.",
    "Budgets, payments and refunds show their exact amounts, including small token fractions. A fee or net payment can have more than two decimal places.",
    "New escrows take a 10% fee from the freelancer’s gross award. A 100-unit award pays the freelancer 90. The client pays no extra platform fee, and refunds have no fee. Old escrows keep their original terms.",
    "The fee is rounded down to the smallest token unit for each payment. You receive the rest. Fractions from different payments are not added together later.",
    "Ask for payment starts a 14 day clock on the escrow. During it the client can still release payment, and you can take the request back. After 14 days a human reviewer can pay you, pay the client back, or split the milestone. The reviewer chooses the split between you and the client; the contract deducts the fixed fee from your share.",
    "Leave this project gives up your unpaid milestones. The money not yet paid goes back to the client. Milestones already paid stay yours.",
    "If the reviewer never answers for 90 days, either of you can split the milestone 50/50 and end the escrow.",
    "While a dispute is open you cannot restart that milestone. Put your best evidence in the delivery note and files, because the AI cannot open links.",
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
