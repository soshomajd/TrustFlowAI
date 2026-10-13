import type { Metadata } from "next";

import { RegisterForm } from
    "@/features/auth/components/register-form";

export const metadata: Metadata = {
    title: "Create account",
    description:
        "Create a free TrustFlow AI account as a client or a freelancer and try milestone escrow with test tokens.",
    alternates: {
        // ?role= only picks which side is selected, so every link is one page.
        canonical: "/register",
    },
};

type RegisterPageProps = {
    searchParams: Promise<{ role?: string }>;
};

export default async function RegisterPage({
    searchParams,
}: RegisterPageProps) {
    const { role } = await searchParams;

    // The landing page links here with the side the visitor picked.
    return (
        <RegisterForm
            defaultRole={role === "Freelancer" ? "Freelancer" : "Client"}
        />
    );
}
