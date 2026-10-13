import type { Metadata } from "next";

import { LoginForm } from
    "@/features/auth/components/login-form";

export const metadata: Metadata = {
    title: "Sign in",
    description:
        "Sign in to TrustFlow AI to fund an escrow, deliver milestones or release payment from your own wallet.",
    alternates: {
        canonical: "/login",
    },
};

export default function LoginPage() {
    return <LoginForm />;
}