import { Suspense } from
    "react";

import {
    MarketplacePageSkeleton,
    MarketplaceProjectsScreen,
} from
    "@/features/projects/components/marketplace-projects-screen";

export default function FreelancerMarketplacePage() {
    return (
        <Suspense
            fallback={
                <MarketplacePageSkeleton />
            }
        >
            <MarketplaceProjectsScreen />
        </Suspense>
    );
}