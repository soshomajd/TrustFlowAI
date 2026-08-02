import { FilePlus2 } from
    "lucide-react";

import { SectionPlaceholder } from
    "@/components/shared/section-placeholder";

export default function CreateProjectPage() {
    return (
        <SectionPlaceholder
            eyebrow="Client workspace"
            title="Create project"
            description="The project creation form and milestone setup will appear here."
            icon={FilePlus2}
        />
    );
}