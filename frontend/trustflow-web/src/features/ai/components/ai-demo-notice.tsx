import { FlaskConical } from "lucide-react";

// Shown whenever the answer on screen came from the placeholder model, so a
// demo can never be mistaken for a real analysis.
export function AiDemoNotice({ isDemo }: { isDemo: boolean }) {
  if (!isDemo) {
    return null;
  }

  return (
    <p
      role="note"
      className="flex gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-xs leading-5 text-amber-200"
    >
      <FlaskConical className="mt-0.5 size-4 shrink-0" />
      Demo mode. The AI is not connected to a real model yet, so this text is a
      placeholder and not a real analysis.
    </p>
  );
}
