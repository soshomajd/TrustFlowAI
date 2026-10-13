import type { Metadata } from "next";

import { RegisterForm } from
    "@/features/auth/components/register-form";

export const metadata: Metadata = {
    title: "Create account",
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
