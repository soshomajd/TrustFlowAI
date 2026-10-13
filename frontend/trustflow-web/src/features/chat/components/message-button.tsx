import { MessageSquare } from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";

// Opens the conversation between a project's client and one freelancer. The
// conversation page decides whether it exists, so this is only a link.
export function MessageButton({
  projectId,
  freelancerId,
  label = "Message",
  className,
}: {
  projectId: string;
  freelancerId: string;
  label?: string;
  className?: string;
}) {
  return (
    <Button asChild variant="outline" size="sm" className={className}>
      <Link href={`/dashboard/messages/${projectId}/${freelancerId}`}>
        <MessageSquare className="size-4" aria-hidden="true" />
        {label}
      </Link>
    </Button>
  );
}
