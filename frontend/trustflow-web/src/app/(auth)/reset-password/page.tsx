import type { Metadata } from "next";

import { ResetPasswordForm } from
    "@/features/auth/components/reset-password-form";

export const metadata: Metadata = {
    title: "Reset password",
    robots: {
        index: false,
        follow: false,
    },
};

type ResetPasswordPageProps = {
    searchParams: Promise<{
        email?: string | string[];
    }>;
};

export default async function ResetPasswordPage({
    searchParams,
}: ResetPasswordPageProps) {
    const { email } = await searchParams;

    return (
        <ResetPasswordForm
            email={typeof email === "string" ? email : ""}
        />
    );
}
