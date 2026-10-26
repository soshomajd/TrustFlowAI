import type { Metadata } from "next";

import { ReviewCaseScreen } from "@/features/review/components/review-case-screen";
import { LocalAdminSessionNotice } from "@/features/admin/components/local-admin-session-notice";

export const metadata: Metadata = {
    title: "Review a case",
};

type ReviewCasePageProps = {
    params: Promise<{
        caseId: string;
    }>;
};

export default async function ReviewCasePage({ params }: ReviewCasePageProps) {
    const { caseId } = await params;

    return <><LocalAdminSessionNotice /><ReviewCaseScreen caseId={caseId} /></>;
}
