import { Suspense } from
    "react";

import {
    MyProposalsPageSkeleton,
    MyProposalsScreen,
} from
    "@/features/proposals/components/my-proposals-screen";

export default function FreelancerProposalsPage() {
    return (
        <Suspense
            fallback={
                <MyProposalsPageSkeleton />
            }
        >
            <MyProposalsScreen />
        </Suspense>
    );
}