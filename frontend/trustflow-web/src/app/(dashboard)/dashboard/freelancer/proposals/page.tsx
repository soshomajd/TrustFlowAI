import { Send } from "lucide-react";

import { SectionPlaceholder } from
    "@/components/shared/section-placeholder";

export default function FreelancerProposalsPage() {
    return (
        <SectionPlaceholder
            eyebrow="Freelancer workspace"
            title="My proposals"
            description="Track pending, accepted, rejected and withdrawn proposals."
            icon={Send}
        />
    );
}