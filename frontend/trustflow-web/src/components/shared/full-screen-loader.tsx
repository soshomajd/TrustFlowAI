import { LoaderCircle } from "lucide-react";

type FullScreenLoaderProps = {
    message?: string;
};

export function FullScreenLoader({
    message = "Loading TrustFlow...",
}: FullScreenLoaderProps) {
    return (
        <div className="flex min-h-screen items-center justify-center px-6">
            <div className="flex flex-col items-center gap-4 text-center">
                <div className="flex size-14 items-center justify-center rounded-2xl border border-primary/30 bg-primary/10 shadow-blue-glow">
                    <LoaderCircle className="size-6 animate-spin text-electric" />
                </div>

                <p className="text-sm text-muted-foreground">
                    {message}
                </p>
            </div>
        </div>
    );
}