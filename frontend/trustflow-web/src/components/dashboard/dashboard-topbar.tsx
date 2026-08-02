"use client";

import {
    Menu,
    PanelLeftClose,
    PanelLeftOpen,
} from "lucide-react";
import { usePathname } from
    "next/navigation";

import { Button } from
    "@/components/ui/button";

import { useAuthStore } from
    "@/stores/auth-store";
import { useUiStore } from
    "@/stores/ui-store";
import {
    getActiveDashboardNavigationItem,
} from "@/config/dashboard-navigation";

export function DashboardTopbar() {
    const pathname = usePathname();

    const user = useAuthStore(
        (state) => state.user,
    );

    const isSidebarCollapsed =
        useUiStore(
            (state) =>
                state.isSidebarCollapsed,
        );

    const toggleSidebar = useUiStore(
        (state) => state.toggleSidebar,
    );

    const setMobileSidebarOpen =
        useUiStore(
            (state) =>
                state.setMobileSidebarOpen,
        );

    const currentNavigationItem =
        getActiveDashboardNavigationItem(
            pathname,
            user?.roles ?? [],
        );

    return (
        <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-border/70 bg-background/80 px-4 backdrop-blur-xl sm:px-6">
            <div className="flex items-center gap-3">
                <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={() =>
                        setMobileSidebarOpen(true)
                    }
                    className="lg:hidden"
                    aria-label="Open navigation"
                >
                    <Menu className="size-5" />
                </Button>

                <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={toggleSidebar}
                    className="hidden lg:inline-flex"
                    aria-label={
                        isSidebarCollapsed
                            ? "Expand sidebar"
                            : "Collapse sidebar"
                    }
                >
                    {isSidebarCollapsed ? (
                        <PanelLeftOpen className="size-5" />
                    ) : (
                        <PanelLeftClose className="size-5" />
                    )}
                </Button>

                <div>
                    <p className="text-sm font-semibold">
                        {currentNavigationItem
                            ?.label ?? "Dashboard"}
                    </p>

                    <p className="hidden text-xs text-muted-foreground sm:block">
                        Welcome back,{" "}
                        {user?.fullName}
                    </p>
                </div>
            </div>

            <div className="flex size-9 items-center justify-center rounded-xl border border-primary/30 bg-primary/10 text-sm font-semibold text-electric">
                {getInitials(
                    user?.fullName,
                )}
            </div>
        </header>
    );
}

function getInitials(
    fullName?: string,
) {
    if (!fullName) {
        return "TF";
    }

    return fullName
        .trim()
        .split(/\s+/)
        .slice(0, 2)
        .map((part) => part[0])
        .join("")
        .toUpperCase();
}