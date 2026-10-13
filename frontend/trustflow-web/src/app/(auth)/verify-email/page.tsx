import type { Metadata } from "next";

import { VerifyEmailForm } from
    "@/features/auth/components/verify-email-form";

export const metadata: Metadata = {
    title: "Verify your email",
    robots: {
        index: false,
        follow: false,
    },
};

type VerifyEmailPageProps = {
    searchParams: Promise<{
        email?: string | string[];
        sent?: string | string[];
    }>;
};

export default async function VerifyEmailPage({
    searchParams,
}: VerifyEmailPageProps) {
    const { email, sent } = await searchParams;

    return (
        <VerifyEmailForm
            email={typeof email === "string" ? email : ""}
            codeJustSent={sent === "true"}
        />
    );
}
