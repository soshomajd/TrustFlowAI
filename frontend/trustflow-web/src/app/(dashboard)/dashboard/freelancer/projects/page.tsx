import { BriefcaseBusiness } from
    "lucide-react";

import { SectionPlaceholder } from
    "@/components/shared/section-placeholder";

export default function AssignedProjectsPage() {
    return (
        <SectionPlaceholder
            eyebrow="Freelancer workspace"
            title="Assigned projects"
            description="Manage active projects and complete milestones in sequence."
            icon={BriefcaseBusiness}
        />
    );
}