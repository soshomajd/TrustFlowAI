import {
  BadgeCheck,
  Banknote,
  Clock3,
  FileSearch,
  Gavel,
  HandCoins,
  LockKeyhole,
  Scale,
  ShieldCheck,
  Sparkles,
  UserRoundCheck,
  Wand2,
  type LucideIcon,
} from "lucide-react";

// Everything on the landing page is a statement about how the product works
// today. There are no customer counts, ratings or testimonials, because the
// product has no users to quote yet.

export type NavLink = { label: string; href: string };

export const navLinks: NavLink[] = [
  { label: "How it works", href: "#how-it-works" },
  { label: "Protection", href: "#protection" },
  { label: "AI help", href: "#ai-help" },
  { label: "FAQ", href: "#faq" },
];

export type TrustPoint = { icon: LucideIcon; label: string };

export const trustPoints: TrustPoint[] = [
  { icon: LockKeyhole, label: "Budget held by a smart contract" },
  { icon: BadgeCheck, label: "You release every milestone" },
  { icon: Clock3, label: "14 day safety window" },
  { icon: Scale, label: "AI advises, a human decides" },
];

export type Problem = {
  icon: LucideIcon;
  before: string;
  after: string;
  detail: string;
};

export const problems: Problem[] = [
  {
    icon: HandCoins,
    before: "Pay first and hope",
    after: "Budget locked, not sent",
    detail:
      "The full budget goes into a contract for this one project. The freelancer can see it is there, and you can see nobody has touched it.",
  },
  {
    icon: UserRoundCheck,
    before: "Work first and hope",
    after: "Paid per milestone",
    detail:
      "Each milestone has its own amount. When you approve a delivery you release that part, and the next stage starts.",
  },
  {
    icon: Gavel,
    before: "Disputes with no end",
    after: "Clear rules and a clock",
    detail:
      "If you cannot agree, either side can start a 14 day request. After it, a human reviewer decides how the milestone is shared.",
  },
];

export type Step = { title: string; text: string };

export const clientSteps: Step[] = [
  {
    title: "Describe the project",
    text: "Write what you need, or let the AI draft the title, price and milestones for you to edit.",
  },
  {
    title: "Choose a freelancer",
    text: "Read proposals and profiles. Hire one person for everything or a different one per milestone.",
  },
  {
    title: "Fund the escrow",
    text: "Sign with your own wallet. The budget moves into the contract, not to us and not to the freelancer.",
  },
  {
    title: "Review and release",
    text: "Check each delivery, with an AI check if you want one. Release payment when it matches what you asked for.",
  },
];

export const freelancerSteps: Step[] = [
  {
    title: "Build your profile",
    text: "Add skills, samples and a resume. The AI coach suggests wording, and you decide what to keep.",
  },
  {
    title: "Pick the milestones you want",
    text: "The client sets the price, so you compete on skill and delivery time, not on who charges least.",
  },
  {
    title: "Start when it is funded",
    text: "You only begin once the escrow says Funded, so the money is already waiting.",
  },
  {
    title: "Deliver and get paid",
    text: "Submit files and notes for each milestone. If the client goes quiet, you can ask for payment.",
  },
];

export type Rule = { icon: LucideIcon; title: string; text: string };

export const rules: Rule[] = [
  {
    icon: ShieldCheck,
    title: "We cannot move your money",
    text: "Funds sit in a contract that only the client and the freelancer can act on. Our servers never hold a key to it.",
  },
  {
    icon: BadgeCheck,
    title: "Only you release your payments",
    text: "A client releases milestones in order, one at a time. Nothing is paid out by accident or by surprise.",
  },
  {
    icon: Clock3,
    title: "Silence is not a trap",
    text: "If the client does not answer, the freelancer can ask for payment. After 14 days a reviewer can step in.",
  },
  {
    icon: Scale,
    title: "A reviewer has narrow powers",
    text: "The reviewer can split one milestone between the two of you. They cannot send money anywhere else.",
  },
  {
    icon: Sparkles,
    title: "The AI never touches funds",
    text: "It reads the work and gives an opinion. It cannot sign, release or refund anything.",
  },
  {
    icon: Banknote,
    title: "No lock-in if someone vanishes",
    text: "A freelancer can leave and the unpaid money returns to you. If the reviewer never answers, either side can split 50/50 after 90 days.",
  },
];

export type Lifecycle = { label: string; note: string };

export const lifecycle: Lifecycle[] = [
  { label: "Fund", note: "Client locks the budget" },
  { label: "Work", note: "Freelancer starts" },
  { label: "Deliver", note: "Files and notes" },
  { label: "Release", note: "Client approves" },
  { label: "Or ask", note: "14 day request" },
  { label: "Decide", note: "Human reviewer" },
];

export type AiFeature = { icon: LucideIcon; title: string; text: string };

export const aiFeatures: AiFeature[] = [
  {
    icon: Wand2,
    title: "Draft a project",
    text: "Describe your idea. Get a title, a description, a fair price and milestones that add up to your budget. Edit anything before you publish.",
  },
  {
    icon: FileSearch,
    title: "Check a delivery",
    text: "Compare the delivered work with each part of your request, and see what is met, partly met or missing, with a note you can send back.",
  },
  {
    icon: Sparkles,
    title: "Coach a profile",
    text: "Freelancers get a profile review, wording ideas and a resume draft from their finished work. Nothing is saved until you save it.",
  },
];

export type Comparison = { row: string; typical: string; trustflow: string };

export const comparison: Comparison[] = [
  {
    row: "Who holds the money",
    typical: "The platform, in its own account",
    trustflow: "A smart contract made for this project",
  },
  {
    row: "Who can release it",
    typical: "The platform, by its own rules",
    trustflow: "The client, and nobody else without a case",
  },
  {
    row: "When you disagree",
    typical: "A support queue, with no set timeline",
    trustflow: "A 14 day request, then a human reviewer",
  },
  {
    row: "What a reviewer can do",
    typical: "Anything the platform allows",
    trustflow: "Split one milestone between the two of you",
  },
  {
    row: "Can you check the rules",
    typical: "Only by trusting the terms",
    trustflow: "Yes, they are written into the contract",
  },
];

export type Faq = { question: string; answer: string };

export const faqs: Faq[] = [
  {
    question: "Is this real money?",
    answer:
      "Not yet. TrustFlow is in early access and runs on a test network with test tokens, so you can try the whole flow without risking funds.",
  },
  {
    question: "Do I need a crypto wallet?",
    answer:
      "Yes. You use your own wallet to fund an escrow or to receive payment. You prove the wallet is yours by signing a message once.",
  },
  {
    question: "What does it cost?",
    answer:
      "This version adds no platform fee. Wallet transactions have a normal network fee that goes to the network, not to us.",
  },
  {
    question: "What if the client disappears after I deliver?",
    answer:
      "Ask for payment from the milestone. The client can still release at any time. After 14 days a reviewer can decide how that milestone is shared.",
  },
  {
    question: "What if the freelancer disappears?",
    answer:
      "Ask for a refund on the milestone, or wait for the freelancer to leave the project. Money that was not paid out returns to you and the unpaid milestones open for hire again.",
  },
  {
    question: "Can the AI be wrong?",
    answer:
      "Yes. It is advice, clearly labelled, and it never moves money. A person makes any decision about funds, and you can always read the evidence yourself.",
  },
  {
    question: "Who is the reviewer?",
    answer:
      "A person or group whose wallet address is written into each contract when it is created. That address can act only on an open case, only after the 14 days.",
  },
];
