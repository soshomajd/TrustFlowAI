import type { Metadata } from "next";

import { ReviewDeskScreen } from "@/features/review/components/review-desk-screen";
import { LocalAdminSessionNotice } from "@/features/admin/components/local-admin-session-notice";

export const metadata: Metadata = {
    title: "Review desk",
};

export default function ReviewDeskPage() {
    return <><LocalAdminSessionNotice /><ReviewDeskScreen /></>;
}
