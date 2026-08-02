"use client";

import type { ReactNode } from "react";

import {
    Sheet,
    SheetContent,
    SheetHeader,
    SheetTitle,
} from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import { DashboardSidebar } from
    "@/components/dashboard/dashboard-sidebar";
import { DashboardTopbar } from
    "@/components/dashboard/dashboard-topbar";
import { useUiStore } from
    "@/stores/ui-store";

type DashboardShellProps = {
    children: ReactNode;
};

export function DashboardShell({
    children,
}: DashboardShellProps) {
    const isSidebarCollapsed =
        useUiStore(
            (state) =>
                state.isSidebarCollapsed,
        );

    const isMobileSidebarOpen =
        useUiStore(
            (state) =>
                state.isMobileSidebarOpen,
        );

    const setMobileSidebarOpen =
        useUiStore(
            (state) =>
                state.setMobileSidebarOpen,
        );

    return (
        <div className="min-h-screen bg-background">
            <aside
                className={cn(
                    "fixed inset-y-0 left-0 z-40 hidden border-r border-sidebar-border transition-[width] duration-300 lg:block",
                    isSidebarCollapsed
                        ? "w-20"
                        : "w-72",
                )}
            >
                <DashboardSidebar />
            </aside>

            <Sheet
                open={isMobileSidebarOpen}
                onOpenChange={
                    setMobileSidebarOpen
                }
            >
                <SheetContent
                    side="left"
                    className="w-72 border-sidebar-border p-0"
                >
                    <SheetHeader className="sr-only">
                        <SheetTitle>
                            Dashboard navigation
                        </SheetTitle>
                    </SheetHeader>

                    <DashboardSidebar mobile />
                </SheetContent>
            </Sheet>

            <div
                className={cn(
                    "min-h-screen transition-[padding] duration-300",
                    isSidebarCollapsed
                        ? "lg:pl-20"
                        : "lg:pl-72",
                )}
            >
                <DashboardTopbar />

                {children}
            </div>
        </div>
    );
}