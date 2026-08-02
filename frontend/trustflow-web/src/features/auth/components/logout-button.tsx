"use client";

import { LoaderCircle,LogOut,} from "lucide-react";
import { Button } from "@/components/ui/button";
import { useLogout } from "@/features/auth/hooks/use-logout";

type LogoutButtonProps = { collapsed?: boolean;};

export function LogoutButton({
    collapsed = false,
}: LogoutButtonProps) {
    const logoutMutation = useLogout();

    return (
        <Button
            type="button"
            variant="ghost"
            disabled={logoutMutation.isPending}
            onClick={() =>
                logoutMutation.mutate()
            }
            className="justify-start text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
        >
            {logoutMutation.isPending ? (
                <LoaderCircle className="size-4 animate-spin" />
            ) : (
                <LogOut className="size-4" />
            )}

            {!collapsed && (
                <span>
                    {logoutMutation.isPending
                        ? "Signing out..."
                        : "Sign out"}
                </span>
            )}
        </Button>
    );
}