import type { Metadata } from "next";

import { ReviewDeskScreen } from "@/features/review/components/review-desk-screen";

export const metadata: Metadata = {
    title: "Review desk",
};

export default function ReviewDeskPage() {
    return <ReviewDeskScreen />;
}
