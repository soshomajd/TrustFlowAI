import { Search } from
    "lucide-react";

import { SectionPlaceholder } from
    "@/components/shared/section-placeholder";

export default function MarketplacePage() {
    return (
        <SectionPlaceholder
            eyebrow="Freelancer workspace"
            title="Project marketplace"
            description="Search, filter and explore open projects from clients."
            icon={Search}
        />
    );
}